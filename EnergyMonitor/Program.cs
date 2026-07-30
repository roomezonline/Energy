using System.Text;
using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Application.Services;
using EnergyMonitor.Data;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Infrastructure.Repositories;
using EnergyMonitor.Infrastructure.Security;
using EnergyMonitor.Infrastructure.Services;
using EnergyMonitor.Middleware;
using EnergyMonitor.Services;
using OldDbContext = EnergyMonitor.Data.AppDbContext;
using ICurrentUserService = EnergyMonitor.Services.ICurrentUserService;
using CurrentUserService = EnergyMonitor.Services.CurrentUserService;
using IPdfReportService = EnergyMonitor.Services.IPdfReportService;
using PdfReportService = EnergyMonitor.Services.PdfReportService;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;
using NewDbContext = EnergyMonitor.Infrastructure.Data.AppDbContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);

var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required");

// Old DbContext (primary — migrations, controllers, services)
builder.Services.AddDbContext<OldDbContext>(opts =>
    opts.UseSqlServer(conn, sqlOpts => sqlOpts.EnableRetryOnFailure()));

// New DbContext (read-only for new repositories — no migrations)
builder.Services.AddDbContext<NewDbContext>(opts =>
    opts.UseSqlServer(conn, sqlOpts => sqlOpts.EnableRetryOnFailure().UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)),
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Scoped);

// Current user service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// PDF report service
QuestPDF.Settings.License = LicenseType.Community;
var fontPath = Path.Combine(AppContext.BaseDirectory, "Fonts");
if (Directory.Exists(fontPath))
{
    QuestPDF.Settings.EnableCaching = true;
    var fontFiles = new[] { "vazirmatn-300.ttf", "vazirmatn-400.ttf", "vazirmatn-600.ttf", "vazirmatn-700.ttf", "vazirmatn-900.ttf" };
    foreach (var f in fontFiles)
        FontManager.RegisterFont(File.OpenRead(Path.Combine(fontPath, f)));
}
builder.Services.AddScoped<IPdfReportService, PdfReportService>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrEmpty(jwtKey))
    throw new InvalidOperationException("Jwt:SecretKey is required in configuration. Set it in appsettings.json or via JWT_SECRET environment variable.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "EnergyMonitor";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "EnergyMonitor";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = builder.Configuration["Jwt:Issuer"] != null,
            ValidateAudience = builder.Configuration["Jwt:Audience"] != null,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// New DI — Repositories
builder.Services.AddScoped<ICenterRepository, CenterRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<ISnapshotRepository, SnapshotRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<ICalibrationLogRepository, CalibrationLogRepository>();
builder.Services.AddScoped<ITariffRepository, TariffRepository>();
builder.Services.AddScoped<IRepository<EnergyMonitor.Domain.Entities.EnergyLimit>, Repository<EnergyMonitor.Domain.Entities.EnergyLimit>>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// New DI — Services

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEnergySnapshotReader, EnergySnapshotReader>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IConsumptionService, ConsumptionService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

// New DI — Cross-cutting
builder.Services.AddSingleton(new JwtTokenGenerator(jwtKey, jwtIssuer, jwtAudience));
builder.Services.AddScoped<ITokenGenerator>(sp => sp.GetRequiredService<JwtTokenGenerator>());
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITimeProvider, IranTimeProvider>();

// Old DI
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<AlarmService>();
builder.Services.AddHostedService<AlarmCleanupService>();
builder.Services.AddHostedService<ConsumptionMonitorService>();

builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.MimeTypes = new[] { "application/octet-stream", "application/wasm", "application/x-dotnet" };
});
builder.Services.AddCors(opts => opts.AddPolicy("All",
    p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Database setup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    var newDb = scope.ServiceProvider.GetRequiredService<NewDbContext>();
    newDb.Database.EnsureCreated();

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<object>>();

    // Add new columns if missing (for existing DB)
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'LastTotalPower')
BEGIN
    ALTER TABLE Centers ADD LastTotalPower float NULL;
    ALTER TABLE Centers ADD LastTotalEnergyKWh float NULL;
    ALTER TABLE Centers ADD LastVoltage float NULL;
    ALTER TABLE Centers ADD LastCurrent float NULL;
    ALTER TABLE Centers ADD LastFrequency float NULL;
    ALTER TABLE Centers ADD LastDataTimestamp datetime2 NULL;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Centers columns"); }

    // Create DeviceConfigs table + add IsSavingEnabled column
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('DeviceConfigs') AND type = 'U')
BEGIN
    CREATE TABLE DeviceConfigs (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        DeviceId nvarchar(450) NOT NULL,
        OverVoltageThreshold float NOT NULL DEFAULT 253,
        UnderVoltageThreshold float NOT NULL DEFAULT 207,
        OverCurrentThreshold float NOT NULL DEFAULT 20,
        PhaseImbalanceThreshold float NOT NULL DEFAULT 15,
        LowPFThreshold float NOT NULL DEFAULT 0.80,
        FreqMinThreshold float NOT NULL DEFAULT 49.5,
        FreqMaxThreshold float NOT NULL DEFAULT 50.5,
        HighPowerThreshold float NOT NULL DEFAULT 5000,
        TemperatureThreshold float NOT NULL DEFAULT 40,
        PublishIntervalMs int NOT NULL DEFAULT 15000,
        IsSavingEnabled bit NOT NULL DEFAULT 1,
        AlarmSoundEnabled bit NOT NULL DEFAULT 1,
        UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE UNIQUE INDEX IX_DeviceConfigs_DeviceId ON DeviceConfigs (DeviceId);
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DeviceConfigs') AND name = 'IsSavingEnabled')
BEGIN
    ALTER TABLE DeviceConfigs ADD IsSavingEnabled bit NOT NULL DEFAULT 1;
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DeviceConfigs') AND name = 'AlarmSoundEnabled')
BEGIN
    ALTER TABLE DeviceConfigs ADD AlarmSoundEnabled bit NOT NULL DEFAULT 1;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: IsSavingEnabled column"); }

    // Add TemperatureThreshold column to DeviceConfigs if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DeviceConfigs') AND name = 'TemperatureThreshold')
BEGIN
    ALTER TABLE DeviceConfigs ADD TemperatureThreshold float NOT NULL DEFAULT 40;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: TemperatureThreshold column"); }

    // Create Devices table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Devices') AND type = 'U')
BEGIN
    CREATE TABLE Devices (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        DeviceId nvarchar(450) NOT NULL,
        DisplayName nvarchar(max) NOT NULL DEFAULT N'',
        MacAddress nvarchar(max) NULL,
        CenterId uniqueidentifier NOT NULL DEFAULT '215f4a2a-5ef2-4ef8-97e9-f717adc7f845',
        IsActive bit NOT NULL DEFAULT 1,
        Location nvarchar(max) NULL,
        LastSeenAt datetime2 NULL,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE UNIQUE INDEX IX_Devices_DeviceId ON Devices (DeviceId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Devices table"); }

    // Add Phase connection status columns to Devices if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Devices') AND name = 'PhaseAConnected')
BEGIN
    ALTER TABLE Devices ADD PhaseAConnected bit NOT NULL DEFAULT 1;
    ALTER TABLE Devices ADD PhaseBConnected bit NOT NULL DEFAULT 1;
    ALTER TABLE Devices ADD PhaseCConnected bit NOT NULL DEFAULT 1;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Phase connection columns"); }

    // Add PhaseCount column (1=single-phase, 3=three-phase)
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Devices') AND name = 'PhaseCount')
BEGIN
    ALTER TABLE Devices ADD PhaseCount int NOT NULL DEFAULT 3;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: PhaseCount column"); }

    // Add EnergyA/B/C columns if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EnergySnapshots') AND name = 'EnergyA')
BEGIN
    ALTER TABLE EnergySnapshots ADD EnergyA float NOT NULL DEFAULT 0;
    ALTER TABLE EnergySnapshots ADD EnergyB float NOT NULL DEFAULT 0;
    ALTER TABLE EnergySnapshots ADD EnergyC float NOT NULL DEFAULT 0;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: EnergyA/B/C columns"); }

    // Add Temperature column to EnergySnapshots if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EnergySnapshots') AND name = 'Temperature')
BEGIN
    ALTER TABLE EnergySnapshots ADD Temperature float NOT NULL DEFAULT 0;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Temperature column"); }

    // Add PersianTimestamp column to EnergySnapshots if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EnergySnapshots') AND name = 'PersianTimestamp')
    ALTER TABLE EnergySnapshots ADD PersianTimestamp nvarchar(max) NOT NULL DEFAULT '';");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: EnergySnapshots PersianTimestamp column"); }

    // Create EnergyConsumptions table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('EnergyConsumptions') AND type = 'U')
BEGIN
    CREATE TABLE EnergyConsumptions (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        DeviceId nvarchar(450) NOT NULL,
        Timestamp datetime2 NOT NULL,
        DeltaA float NOT NULL DEFAULT 0,
        PeakCurrentA float NOT NULL DEFAULT 0,
        PeakPowerA float NOT NULL DEFAULT 0,
        DeltaB float NOT NULL DEFAULT 0,
        PeakCurrentB float NOT NULL DEFAULT 0,
        PeakPowerB float NOT NULL DEFAULT 0,
        DeltaC float NOT NULL DEFAULT 0,
        PeakCurrentC float NOT NULL DEFAULT 0,
        PeakPowerC float NOT NULL DEFAULT 0,
        IsBackfill bit NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX IX_EnergyConsumptions_DeviceId_Timestamp ON EnergyConsumptions (DeviceId, Timestamp);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: EnergyConsumptions table"); }

    // Add IsBackfill column if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EnergyConsumptions') AND name = 'IsBackfill')
    ALTER TABLE EnergyConsumptions ADD IsBackfill bit NOT NULL DEFAULT 0;");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: IsBackfill column"); }

    // Add PersianTimestamp column to EnergyConsumptions if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('EnergyConsumptions') AND name = 'PersianTimestamp')
    ALTER TABLE EnergyConsumptions ADD PersianTimestamp nvarchar(max) NOT NULL DEFAULT '';");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: EnergyConsumptions PersianTimestamp column"); }

    // Create EnergyLimits table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('EnergyLimits') AND type = 'U')
BEGIN
    CREATE TABLE EnergyLimits (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        CenterId uniqueidentifier NOT NULL,
        LimitType nvarchar(50) NOT NULL,
        PeriodType nvarchar(50) NOT NULL,
        MaxValue float NOT NULL DEFAULT 0,
        AlertThresholdPercent float NOT NULL DEFAULT 80,
        IsActive bit NOT NULL DEFAULT 1
    );
    CREATE INDEX IX_EnergyLimits_CenterId ON EnergyLimits (CenterId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: EnergyLimits table"); }

    // Create EnergySnapshots table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('EnergySnapshots') AND type = 'U')
BEGIN
    CREATE TABLE EnergySnapshots (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        DeviceId nvarchar(450) NOT NULL,
        Timestamp datetime2 NOT NULL,
        VoltageA float NOT NULL DEFAULT 0,
        CurrentA float NOT NULL DEFAULT 0,
        PowerA float NOT NULL DEFAULT 0,
        PfA float NOT NULL DEFAULT 0,
        VoltageB float NOT NULL DEFAULT 0,
        CurrentB float NOT NULL DEFAULT 0,
        PowerB float NOT NULL DEFAULT 0,
        PfB float NOT NULL DEFAULT 0,
        VoltageC float NOT NULL DEFAULT 0,
        CurrentC float NOT NULL DEFAULT 0,
        PowerC float NOT NULL DEFAULT 0,
        PfC float NOT NULL DEFAULT 0,
        Frequency float NOT NULL DEFAULT 0,
        TotalPower float NOT NULL DEFAULT 0,
        TotalEnergyKWh float NOT NULL DEFAULT 0,
        OverVoltage bit NOT NULL DEFAULT 0,
        OverCurrent bit NOT NULL DEFAULT 0,
        PhaseImbalance bit NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX IX_EnergySnapshots_DeviceId_Timestamp ON EnergySnapshots (DeviceId, Timestamp);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: EnergySnapshots table"); }

    // Remove old Tehran center, keep only Ramhormoz
    try
    {
        db.Database.ExecuteSqlRaw(@"
DELETE FROM AlarmLogs WHERE CenterId = '00000000-0000-0000-0000-000000000001';
DELETE FROM Users WHERE CenterId = '00000000-0000-0000-0000-000000000001';
DELETE FROM Centers WHERE Id = '00000000-0000-0000-0000-000000000001';
UPDATE Centers SET Code = 'AYSAD-001'
WHERE Id = '215f4a2a-5ef2-4ef8-97e9-f717adc7f845';");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Cleanup old center"); }

    // Add ImageFileName column to Centers if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'ImageFileName')
    ALTER TABLE Centers ADD ImageFileName nvarchar(max) NULL;");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: ImageFileName column"); }

    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'TariffId')
    ALTER TABLE Centers ADD TariffId uniqueidentifier NULL;");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: TariffId column"); }

    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'CreatedAt')
    ALTER TABLE Centers ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Centers CreatedAt column"); }

    // Add ResolvedAt + DeviceId columns + DeviceId index to AlarmLogs if missing
    try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AlarmLogs') AND name = 'ResolvedAt') ALTER TABLE AlarmLogs ADD ResolvedAt datetime2 NULL"); } catch (Exception ex) { logger.LogWarning(ex, "Migration: ResolvedAt"); }
    try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AlarmLogs') AND name = 'DeviceId') ALTER TABLE AlarmLogs ADD DeviceId nvarchar(450) NULL"); logger.LogInformation("Migration: DeviceId column OK"); } catch (Exception ex) { logger.LogWarning(ex, "Migration: DeviceId column"); }
    try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AlarmLogs') AND name = 'IX_AlarmLogs_DeviceId') CREATE INDEX IX_AlarmLogs_DeviceId ON AlarmLogs (DeviceId) WHERE DeviceId IS NOT NULL"); } catch (Exception ex) { logger.LogWarning(ex, "Migration: DeviceId index"); }

    // Create NewsArticles table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('NewsArticles') AND type = 'U')
BEGIN
    CREATE TABLE NewsArticles (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Title nvarchar(max) NOT NULL DEFAULT N'',
        Summary nvarchar(max) NOT NULL DEFAULT N'',
        FullText nvarchar(max) NULL,
        ImageFileName nvarchar(max) NULL,
        IsActive bit NOT NULL DEFAULT 1,
        SortOrder int NOT NULL DEFAULT 0,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt datetime2 NULL
    );
    CREATE INDEX IX_NewsArticles_SortOrder ON NewsArticles (SortOrder);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: NewsArticles table"); }

    // Create SliderImages table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SliderImages') AND type = 'U')
BEGIN
    CREATE TABLE SliderImages (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Title nvarchar(max) NULL,
        ImageUrl nvarchar(max) NULL,
        SortOrder int NOT NULL DEFAULT 0,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_SliderImages_SortOrder ON SliderImages (SortOrder);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: SliderImages table"); }

    // Create Tariffs table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Tariffs') AND type = 'U')
BEGIN
    CREATE TABLE Tariffs (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(max) NOT NULL DEFAULT N'',
        Description nvarchar(max) NULL,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),

        SummerOffPeakStart nvarchar(5) NOT NULL DEFAULT '23:00',
        SummerOffPeakEnd nvarchar(5) NOT NULL DEFAULT '06:00',
        SummerMidPeakStart nvarchar(5) NOT NULL DEFAULT '06:00',
        SummerMidPeakEnd nvarchar(5) NOT NULL DEFAULT '12:00',
        SummerPeakStart nvarchar(5) NOT NULL DEFAULT '12:00',
        SummerPeakEnd nvarchar(5) NOT NULL DEFAULT '23:00',

        WinterOffPeakStart nvarchar(5) NOT NULL DEFAULT '23:00',
        WinterOffPeakEnd nvarchar(5) NOT NULL DEFAULT '06:00',
        WinterMidPeakStart nvarchar(5) NOT NULL DEFAULT '06:00',
        WinterMidPeakEnd nvarchar(5) NOT NULL DEFAULT '17:00',
        WinterPeakStart nvarchar(5) NOT NULL DEFAULT '17:00',
        WinterPeakEnd nvarchar(5) NOT NULL DEFAULT '23:00',

        OffPeakRate decimal(18,4) NOT NULL DEFAULT 0,
        MidPeakRate decimal(18,4) NOT NULL DEFAULT 0,
        PeakRate decimal(18,4) NOT NULL DEFAULT 0,

            EffectiveFrom nvarchar(10) NULL,
            EffectiveTo nvarchar(10) NULL,
            MonthlyFixedFee decimal(18,2) NOT NULL DEFAULT 121279,
        ReactivePenaltyThreshold decimal(5,3) NOT NULL DEFAULT 0.9,
        ReactiveBonusThreshold decimal(5,3) NOT NULL DEFAULT 0.95,
        ReactivePenaltyMultiplier decimal(5,2) NOT NULL DEFAULT 3
    );
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Tariffs table"); }

    // Add EffectiveFrom/EffectiveTo to existing Tariffs table
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tariffs') AND name = 'EffectiveFrom')
BEGIN
    ALTER TABLE Tariffs ADD EffectiveFrom nvarchar(10) NULL;
    ALTER TABLE Tariffs ADD EffectiveTo nvarchar(10) NULL;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: EffectiveFrom/To columns"); }

    // Create TariffRates table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('TariffRates') AND type = 'U')
BEGIN
    CREATE TABLE TariffRates (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        TariffId uniqueidentifier NOT NULL,
        Phase nvarchar(5) NOT NULL DEFAULT N'',
        PeriodType nvarchar(20) NOT NULL DEFAULT N'',
        RatePerKWh decimal(18,4) NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_TariffRates_TariffId ON TariffRates (TariffId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: TariffRates table"); }

    // Create Invoices table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Invoices') AND type = 'U')
BEGIN
    CREATE TABLE Invoices (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        CenterId uniqueidentifier NOT NULL,
        TariffId uniqueidentifier NOT NULL,
        FromDate nvarchar(10) NOT NULL DEFAULT N'',
        ToDate nvarchar(10) NOT NULL DEFAULT N'',
        Days int NOT NULL DEFAULT 0,
        Months int NOT NULL DEFAULT 0,
        TotalKWh decimal(14,4) NOT NULL DEFAULT 0,
        EnergyCost decimal(18,2) NOT NULL DEFAULT 0,
        MonthlyFixedFeeTotal decimal(18,2) NOT NULL DEFAULT 0,
        ReactivePenalty decimal(18,2) NOT NULL DEFAULT 0,
        SubTotal decimal(18,2) NOT NULL DEFAULT 0,
        TaxAmount decimal(18,2) NOT NULL DEFAULT 0,
        GrandTotal decimal(18,2) NOT NULL DEFAULT 0,
        Status nvarchar(20) NOT NULL DEFAULT N'Draft',
        Notes nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_Invoices_CenterId ON Invoices (CenterId);
    CREATE INDEX IX_Invoices_TariffId ON Invoices (TariffId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Invoices table"); }

    // Create InvoiceDetails table if missing
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('InvoiceDetails') AND type = 'U')
BEGIN
    CREATE TABLE InvoiceDetails (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        InvoiceId uniqueidentifier NOT NULL,
        Phase nvarchar(5) NOT NULL DEFAULT N'',
        PeriodType nvarchar(20) NOT NULL DEFAULT N'',
        KWh decimal(14,4) NOT NULL DEFAULT 0,
        RatePerKWh decimal(18,4) NOT NULL DEFAULT 0,
        Amount decimal(18,2) NOT NULL DEFAULT 0,
        Penalty decimal(18,2) NULL
    );
    CREATE INDEX IX_InvoiceDetails_InvoiceId ON InvoiceDetails (InvoiceId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: InvoiceDetails table"); }

    // Create PhaseReadings table (new — per-phase PZEM data)
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('PhaseReadings') AND type = 'U')
BEGIN
    CREATE TABLE PhaseReadings (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        SnapshotId uniqueidentifier NOT NULL,
        Phase nvarchar(5) NOT NULL DEFAULT N'',
        EnergyKWh decimal(14,4) NOT NULL DEFAULT 0,
        Power decimal(14,2) NOT NULL DEFAULT 0,
        Voltage decimal(8,2) NOT NULL DEFAULT 0,
        [Current] decimal(10,3) NOT NULL DEFAULT 0,
        Pf decimal(5,4) NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_PhaseReadings_SnapshotId ON PhaseReadings (SnapshotId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: PhaseReadings table"); }

    // Create TariffSnapshots table (new — rate versioning at invoice time)
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('TariffSnapshots') AND type = 'U')
BEGIN
    CREATE TABLE TariffSnapshots (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        InvoiceId uniqueidentifier NOT NULL,
        TariffId uniqueidentifier NOT NULL,
        Phase nvarchar(5) NOT NULL DEFAULT N'',
        PeriodType nvarchar(20) NOT NULL DEFAULT N'',
        RatePerKWh decimal(18,4) NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_TariffSnapshots_InvoiceId ON TariffSnapshots (InvoiceId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: TariffSnapshots table"); }

    // Create CalibrationLogs table (new — audit trail for calibration changes)
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('CalibrationLogs') AND type = 'U')
BEGIN
    CREATE TABLE CalibrationLogs (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        DeviceId nvarchar(450) NOT NULL,
        Action nvarchar(100) NOT NULL DEFAULT N'',
        OldValueType nvarchar(100) NOT NULL DEFAULT N'',
        OldValue nvarchar(max) NULL,
        NewValue nvarchar(max) NULL,
        PerformedBy nvarchar(200) NOT NULL DEFAULT N'',
        PerformedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
        Remarks nvarchar(max) NULL
    );
    CREATE INDEX IX_CalibrationLogs_DeviceId ON CalibrationLogs (DeviceId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: CalibrationLogs table"); }

    // Drop removed Center columns (DeviceId + data fields migrated to Devices/Snapshots)
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Centers_DeviceId')
    DROP INDEX IX_Centers_DeviceId ON Centers;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'DeviceId')
    ALTER TABLE Centers DROP COLUMN DeviceId;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'LastTotalPower')
    ALTER TABLE Centers DROP COLUMN LastTotalPower;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'LastTotalEnergyKWh')
    ALTER TABLE Centers DROP COLUMN LastTotalEnergyKWh;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'LastVoltage')
    ALTER TABLE Centers DROP COLUMN LastVoltage;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'LastCurrent')
    ALTER TABLE Centers DROP COLUMN LastCurrent;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'LastFrequency')
    ALTER TABLE Centers DROP COLUMN LastFrequency;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'LastDataTimestamp')
    ALTER TABLE Centers DROP COLUMN LastDataTimestamp;");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Drop old Center columns"); }

    // Create Regions table
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Regions') AND type = 'U')
BEGIN
    CREATE TABLE Regions (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(200) NOT NULL DEFAULT N'',
        Code nvarchar(20) NOT NULL DEFAULT N'',
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE UNIQUE INDEX IX_Regions_Code ON Regions (Code) WHERE Code != N'';
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Regions table"); }

    // Create Provinces table
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Provinces') AND type = 'U')
BEGIN
    CREATE TABLE Provinces (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(200) NOT NULL DEFAULT N'',
        Code nvarchar(20) NOT NULL DEFAULT N'',
        RegionId uniqueidentifier NOT NULL,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE UNIQUE INDEX IX_Provinces_Code ON Provinces (Code) WHERE Code != N'';
    CREATE INDEX IX_Provinces_RegionId ON Provinces (RegionId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Provinces table"); }

    // Create Cities table
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Cities') AND type = 'U')
BEGIN
    CREATE TABLE Cities (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(200) NOT NULL DEFAULT N'',
        Code nvarchar(20) NOT NULL DEFAULT N'',
        ProvinceId uniqueidentifier NOT NULL,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE UNIQUE INDEX IX_Cities_Code ON Cities (Code) WHERE Code != N'';
    CREATE INDEX IX_Cities_ProvinceId ON Cities (ProvinceId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Cities table"); }

    // Create DeviceGroups table + add columns to Centers/Devices/Users
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('DeviceGroups') AND type = 'U')
BEGIN
    CREATE TABLE DeviceGroups (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(200) NOT NULL DEFAULT N'',
        CenterId uniqueidentifier NOT NULL,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_DeviceGroups_CenterId ON DeviceGroups (CenterId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: DeviceGroups table"); }

    // Add CityId to Centers, RegionId to Users, ApiKeyHash + DeviceGroupId to Devices
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'CityId')
    ALTER TABLE Centers ADD CityId uniqueidentifier NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegionId')
    ALTER TABLE Users ADD RegionId uniqueidentifier NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Devices') AND name = 'ApiKeyHash')
    ALTER TABLE Devices ADD ApiKeyHash nvarchar(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Devices') AND name = 'DeviceGroupId')
    ALTER TABLE Devices ADD DeviceGroupId uniqueidentifier NULL;");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: CityId/RegionId/ApiKey columns"); }

    // Create TieredRates table + add Demand columns to Tariffs
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('TieredRates') AND type = 'U')
BEGIN
    CREATE TABLE TieredRates (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        TariffId uniqueidentifier NOT NULL,
        PeriodType nvarchar(20) NOT NULL DEFAULT N'',
        TierFrom decimal(18,2) NOT NULL DEFAULT 0,
        TierTo decimal(18,2) NULL,
        RatePerKWh decimal(18,4) NOT NULL DEFAULT 0,
        SortOrder int NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_TieredRates_TariffId ON TieredRates (TariffId);
END
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tariffs') AND name = 'DemandRate')
BEGIN
    ALTER TABLE Tariffs ADD DemandRate decimal(18,2) NOT NULL DEFAULT 0;
    ALTER TABLE Tariffs ADD DemandChargeEnabled bit NOT NULL DEFAULT 0;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: TieredRates table + Demand columns"); }

    // Ensure Users table has columns needed by new DbContext
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsActive')
    ALTER TABLE Users ADD IsActive bit NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LastLoginAt')
    ALTER TABLE Users ADD LastLoginAt datetime2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CreatedAt')
    ALTER TABLE Users ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Users columns"); }

    // Create ConsumerTypes table + seed default types
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ConsumerTypes') AND type = 'U')
BEGIN
    CREATE TABLE ConsumerTypes (
        Code nvarchar(20) NOT NULL PRIMARY KEY,
        Name nvarchar(200) NOT NULL DEFAULT N'',
        Description nvarchar(max) NULL,
        Category nvarchar(20) NOT NULL DEFAULT N'Industrial',
        BillingModel nvarchar(20) NOT NULL DEFAULT N'TOU',
        HasTOU bit NOT NULL DEFAULT 1,
        HasTieredRates bit NOT NULL DEFAULT 0,
        SortOrder int NOT NULL DEFAULT 0,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    INSERT INTO ConsumerTypes (Code, Name, Category, BillingModel, HasTOU, HasTieredRates, SortOrder) VALUES
        ('1', N'خانگی', 'Residential', 'Tiered', 0, 1, 1),
        ('2', N'تجاری', 'Commercial', 'TOU', 1, 0, 2),
        ('4-ALEF', N'صنعتی ۴-الف (قدرت زیر ۱MW)', 'Industrial', 'TOU', 1, 0, 3),
        ('4-BE', N'صنعتی ۴-ب', 'Industrial', 'TOU', 1, 0, 4),
        ('4-JIM', N'صنعتی ۴-ج', 'Industrial', 'TOU', 1, 0, 5),
        ('4-DAL', N'صنعتی ۴-د (فولاد/مس/پتروشیمی)', 'Industrial', 'TOU', 1, 0, 6),
        ('4-HE', N'صنعتی ۴-ه', 'Industrial', 'TOU', 1, 0, 7),
        ('3', N'کشاورزی', 'Agricultural', 'TOU', 1, 0, 8),
        ('OTHER', N'سایر', 'Other', 'Flat', 0, 0, 9);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: ConsumerTypes table"); }

    // Create YearlyBaseRates table + seed 1405
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('YearlyBaseRates') AND type = 'U')
BEGIN
    CREATE TABLE YearlyBaseRates (
        [Year] int NOT NULL PRIMARY KEY,
        BaseRatePerKwh decimal(14,2) NOT NULL DEFAULT 0,
        Currency nvarchar(10) NOT NULL DEFAULT N'Rial',
        SourceDocument nvarchar(500) NULL,
        Description nvarchar(max) NULL,
        IsActive bit NOT NULL DEFAULT 1,
        CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
    );
    INSERT INTO YearlyBaseRates ([Year], BaseRatePerKwh, SourceDocument, Description) VALUES
        (1405, 14420, N'ابلاغیه ۱۴۰۴/۱۱۰۴۱۵/۱۰۰', N'متوسط نرخ قراردادهای تبدیل انرژی (ECA) سال ۱۴۰۵');
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: YearlyBaseRates table"); }

    // Create ConsumerTypeYearlyConfigs table + seed 1405 configs
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ConsumerTypeYearlyConfigs') AND type = 'U')
BEGIN
    CREATE TABLE ConsumerTypeYearlyConfigs (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        ConsumerTypeCode nvarchar(20) NOT NULL,
        [Year] int NOT NULL,
        EcaCoefficient decimal(10,4) NOT NULL DEFAULT 1.0,
        MinPowerMW decimal(18,2) NULL,
        MaxPowerMW decimal(18,2) NULL,
        TouOffPeakMultiplier decimal(5,3) NOT NULL DEFAULT 0.5,
        TouMidPeakMultiplier decimal(5,3) NOT NULL DEFAULT 1.0,
        TouPeakMultiplier decimal(5,3) NOT NULL DEFAULT 2.0,
        SummerOffPeakStart nvarchar(5) NOT NULL DEFAULT '23:00',
        SummerOffPeakEnd nvarchar(5) NOT NULL DEFAULT '06:00',
        SummerMidPeakStart nvarchar(5) NOT NULL DEFAULT '06:00',
        SummerMidPeakEnd nvarchar(5) NOT NULL DEFAULT '12:00',
        SummerPeakStart nvarchar(5) NOT NULL DEFAULT '12:00',
        SummerPeakEnd nvarchar(5) NOT NULL DEFAULT '23:00',
        WinterOffPeakStart nvarchar(5) NOT NULL DEFAULT '23:00',
        WinterOffPeakEnd nvarchar(5) NOT NULL DEFAULT '06:00',
        WinterMidPeakStart nvarchar(5) NOT NULL DEFAULT '06:00',
        WinterMidPeakEnd nvarchar(5) NOT NULL DEFAULT '17:00',
        WinterPeakStart nvarchar(5) NOT NULL DEFAULT '17:00',
        WinterPeakEnd nvarchar(5) NOT NULL DEFAULT '23:00',
        MonthlyFixedFee decimal(14,2) NOT NULL DEFAULT 121279,
        ReactivePenaltyThreshold decimal(5,3) NOT NULL DEFAULT 0.91,
        ReactiveBonusThreshold decimal(5,3) NOT NULL DEFAULT 0.95,
        ReactivePenaltyMultiplier decimal(5,2) NOT NULL DEFAULT 3,
        DemandChargeEnabled bit NOT NULL DEFAULT 0,
        DemandRate decimal(14,2) NOT NULL DEFAULT 0,
        Article16Enabled bit NOT NULL DEFAULT 0,
        Article16Percent decimal(5,2) NOT NULL DEFAULT 4,
        Article16GreenEnergyRate decimal(14,2) NOT NULL DEFAULT 63850,
        PeakPenaltyCoefficient decimal(10,4) NOT NULL DEFAULT 0.44,
        PeakPenaltyNormalCoefficient decimal(10,4) NOT NULL DEFAULT 0.146,
        OffPeakDiscountCoefficient decimal(10,4) NOT NULL DEFAULT 0.073,
        OffPeakDiscountTwoRateCoefficient decimal(10,4) NOT NULL DEFAULT 0.0292,
        OverloadViolationMultiplier decimal(5,2) NOT NULL DEFAULT 1.3,
        TaxPercent decimal(5,2) NOT NULL DEFAULT 9,
        TollPercent decimal(5,2) NOT NULL DEFAULT 10,
        VoltageDiscountJson nvarchar(max) NULL
    );
    CREATE INDEX IX_ConsumerTypeYearlyConfigs_Type_Year ON ConsumerTypeYearlyConfigs (ConsumerTypeCode, [Year]);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: ConsumerTypeYearlyConfigs table"); }

    // Seed 1405 configs for default types
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM ConsumerTypeYearlyConfigs WHERE ConsumerTypeCode = '4-ALEF' AND [Year] = 1405)
BEGIN
    INSERT INTO ConsumerTypeYearlyConfigs (Id, ConsumerTypeCode, [Year], EcaCoefficient, MonthlyFixedFee, Article16Enabled, Article16Percent)
    VALUES
        (NEWID(), '4-ALEF', 1405, 0.25, 121279, 0, 0),
        (NEWID(), '4-DAL',  1405, 1.62, 121279, 1, 4),
        (NEWID(), '4-BE',   1405, 0.58, 121279, 0, 0),
        (NEWID(), '4-JIM',  1405, 0.70, 121279, 0, 0),
        (NEWID(), '4-HE',   1405, 2.00, 121279, 1, 4),
        (NEWID(), '1',      1405, 1.00, 0,      0, 0),
        (NEWID(), '2',      1405, 1.20, 50000,  0, 0),
        (NEWID(), '3',      1405, 0.50, 20000,  0, 0),
        (NEWID(), 'OTHER',  1405, 1.00, 50000,  0, 0);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Seed 1405 configs"); }

    // Add new columns to Tariffs
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tariffs') AND name = 'RateDerivationMode')
BEGIN
    ALTER TABLE Tariffs ADD RateDerivationMode nvarchar(20) NOT NULL DEFAULT 'Manual';
    ALTER TABLE Tariffs ADD ConsumerTypeCode nvarchar(20) NULL;
    ALTER TABLE Tariffs ADD [Year] int NULL;
END");
        // Drop VoltageLevelKV column (removed from entity)
        try { db.Database.ExecuteSqlRaw("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tariffs') AND name = 'VoltageLevelKV') ALTER TABLE Tariffs DROP COLUMN VoltageLevelKV"); } catch { }
        try { db.Database.ExecuteSqlRaw("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'VoltageLevelKV') ALTER TABLE Centers DROP COLUMN VoltageLevelKV"); } catch { }
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Tariffs new columns"); }

    // Fix: convert Tariff rate columns from float to decimal (created by EF Core auto-migration)
    try
    {
        db.Database.ExecuteSqlRaw(@"
BEGIN TRY ALTER TABLE Tariffs ALTER COLUMN OffPeakRate decimal(18,4) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Tariffs ALTER COLUMN MidPeakRate decimal(18,4) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Tariffs ALTER COLUMN PeakRate decimal(18,4) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Tariffs ALTER COLUMN MonthlyFixedFee decimal(18,2) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Tariffs ALTER COLUMN ReactivePenaltyThreshold decimal(5,3) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Tariffs ALTER COLUMN ReactiveBonusThreshold decimal(5,3) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Tariffs ALTER COLUMN ReactivePenaltyMultiplier decimal(5,2) NOT NULL; END TRY BEGIN CATCH END CATCH");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Tariff columns to decimal"); }

    // Add new columns to Centers
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'ConsumerTypeCode')
BEGIN
    ALTER TABLE Centers ADD ConsumerTypeCode nvarchar(20) NULL;
    ALTER TABLE Centers ADD ContractCapacityMW decimal(10,4) NULL;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Centers new columns"); }

    // Add new columns to TariffSnapshots
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TariffSnapshots') AND name = 'ConsumerTypeCode')
BEGIN
    ALTER TABLE TariffSnapshots ADD ConsumerTypeCode nvarchar(20) NULL;
    ALTER TABLE TariffSnapshots ADD ConsumerTypeName nvarchar(200) NULL;
    ALTER TABLE TariffSnapshots ADD [Year] int NULL;
    ALTER TABLE TariffSnapshots ADD BaseEcaRate decimal(14,2) NULL;
    ALTER TABLE TariffSnapshots ADD EcaCoefficient decimal(10,4) NULL;
    ALTER TABLE TariffSnapshots ADD TouOffPeakMultiplier decimal(5,3) NULL;
    ALTER TABLE TariffSnapshots ADD TouMidPeakMultiplier decimal(5,3) NULL;
    ALTER TABLE TariffSnapshots ADD TouPeakMultiplier decimal(5,3) NULL;
    ALTER TABLE TariffSnapshots ADD EffectiveOffPeakRate decimal(14,2) NULL;
    ALTER TABLE TariffSnapshots ADD EffectiveMidPeakRate decimal(14,2) NULL;
    ALTER TABLE TariffSnapshots ADD EffectivePeakRate decimal(14,2) NULL;
    ALTER TABLE TariffSnapshots ADD PeakPenaltyAmount decimal(18,2) NULL;
    ALTER TABLE TariffSnapshots ADD OffPeakDiscountAmount decimal(18,2) NULL;
    ALTER TABLE TariffSnapshots ADD Article16Amount decimal(18,2) NULL;
    ALTER TABLE TariffSnapshots ADD DemandCost decimal(18,2) NULL;
    ALTER TABLE TariffSnapshots ADD TotalPenaltyBeforeTax decimal(18,2) NULL;
    ALTER TABLE TariffSnapshots ADD OverrideDetailsJson nvarchar(max) NULL;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: TariffSnapshots new columns"); }

    // Add new columns to Invoices
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'PeakPenalty')
BEGIN
    ALTER TABLE Invoices ADD PeakPenalty decimal(18,2) NULL;
    ALTER TABLE Invoices ADD OffPeakDiscount decimal(18,2) NULL;
    ALTER TABLE Invoices ADD Article16Cost decimal(18,2) NULL;
    ALTER TABLE Invoices ADD DemandCost decimal(18,2) NULL;
    ALTER TABLE Invoices ADD TollAmount decimal(18,2) NULL;
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Invoices new columns"); }

    // Add missing Invoice columns from Domain entity
    try
    {
        db.Database.ExecuteSqlRaw(
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'CreatedByUserId') ALTER TABLE Invoices ADD CreatedByUserId uniqueidentifier NULL;" +
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'IdempotencyKey') ALTER TABLE Invoices ADD IdempotencyKey uniqueidentifier NULL;" +
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'InvoiceNumber') ALTER TABLE Invoices ADD InvoiceNumber nvarchar(100) NOT NULL DEFAULT N'';");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Invoices missing columns"); }

    // Fix: convert Invoice decimal columns from float to decimal
    try
    {
        db.Database.ExecuteSqlRaw(@"
BEGIN TRY ALTER TABLE Invoices ALTER COLUMN TotalKWh decimal(14,4) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Invoices ALTER COLUMN EnergyCost decimal(18,2) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Invoices ALTER COLUMN MonthlyFixedFeeTotal decimal(18,2) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Invoices ALTER COLUMN ReactivePenalty decimal(18,2) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Invoices ALTER COLUMN SubTotal decimal(18,2) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Invoices ALTER COLUMN TaxAmount decimal(18,2) NOT NULL; END TRY BEGIN CATCH END CATCH
BEGIN TRY ALTER TABLE Invoices ALTER COLUMN GrandTotal decimal(18,2) NOT NULL; END TRY BEGIN CATCH END CATCH");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: Invoices columns to decimal"); }

    // Create TariffOverrides table
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('TariffOverrides') AND type = 'U')
BEGIN
    CREATE TABLE TariffOverrides (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        TariffId uniqueidentifier NOT NULL,
        FieldName nvarchar(100) NOT NULL DEFAULT N'',
        OverrideValue decimal(18,2) NOT NULL DEFAULT 0,
        IsPercentage bit NOT NULL DEFAULT 0,
        Reason nvarchar(max) NULL
    );
    CREATE INDEX IX_TariffOverrides_TariffId ON TariffOverrides (TariffId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: TariffOverrides table"); }

    // Create ConsumerTypeTieredRates table
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ConsumerTypeTieredRates') AND type = 'U')
BEGIN
    CREATE TABLE ConsumerTypeTieredRates (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        ConsumerTypeYearlyConfigId uniqueidentifier NOT NULL,
        TierFrom decimal(18,2) NOT NULL DEFAULT 0,
        TierTo decimal(18,2) NOT NULL DEFAULT 0,
        Coefficient decimal(10,4) NULL,
        RatePerKwh decimal(18,4) NOT NULL DEFAULT 0,
        SortOrder int NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_ConsumerTypeTieredRates_ConfigId ON ConsumerTypeTieredRates (ConsumerTypeYearlyConfigId);
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Migration: ConsumerTypeTieredRates table"); }

    // Add SupplyCostPerKwh to YearlyBaseRates
    try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('YearlyBaseRates') AND name = 'SupplyCostPerKwh') EXEC('ALTER TABLE YearlyBaseRates ADD SupplyCostPerKwh decimal(14,2) NOT NULL DEFAULT 0')"); } catch (Exception ex) { logger.LogWarning(ex, "Migration: YearlyBaseRates ADD SupplyCostPerKwh"); }
    try { db.Database.ExecuteSqlRaw("UPDATE YearlyBaseRates SET SupplyCostPerKwh = 9537 WHERE [Year] = 1405"); } catch (Exception ex) { logger.LogWarning(ex, "Migration: YearlyBaseRates UPDATE SupplyCostPerKwh"); }

    // Add Coefficient to ConsumerTypeTieredRates
    try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ConsumerTypeTieredRates') AND name = 'Coefficient') EXEC('ALTER TABLE ConsumerTypeTieredRates ADD Coefficient decimal(10,4) NULL')"); } catch (Exception ex) { logger.LogWarning(ex, "Migration: ConsumerTypeTieredRates ADD Coefficient"); }

    // Add ConsumptionPatternKWh to ConsumerTypeYearlyConfigs
    try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ConsumerTypeYearlyConfigs') AND name = 'ConsumptionPatternKWh') EXEC('ALTER TABLE ConsumerTypeYearlyConfigs ADD ConsumptionPatternKWh decimal(10,2) NULL')"); } catch (Exception ex) { logger.LogWarning(ex, "Migration: ConsumerTypeYearlyConfigs ADD ConsumptionPatternKWh"); }
    try { db.Database.ExecuteSqlRaw("UPDATE ConsumerTypeYearlyConfigs SET ConsumptionPatternKWh = 200 WHERE ConsumerTypeCode = '1' AND [Year] = 1405 AND ConsumptionPatternKWh IS NULL"); } catch (Exception ex) { logger.LogWarning(ex, "Migration: ConsumerTypeYearlyConfigs UPDATE ConsumptionPatternKWh"); }

    // Add ConsumptionPatternKWh to Centers
    try { db.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Centers') AND name = 'ConsumptionPatternKWh') EXEC('ALTER TABLE Centers ADD ConsumptionPatternKWh decimal(10,2) NULL')"); } catch (Exception ex) { logger.LogWarning(ex, "Migration: Centers ADD ConsumptionPatternKWh"); }

    // Seed residential tiered rates for 1405 (ConsumerTypeCode '1')
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM ConsumerTypeTieredRates r JOIN ConsumerTypeYearlyConfigs c ON r.ConsumerTypeYearlyConfigId = c.Id WHERE c.ConsumerTypeCode = '1' AND c.[Year] = 1405)
BEGIN
    DECLARE @cfgId uniqueidentifier;
    SELECT @cfgId = Id FROM ConsumerTypeYearlyConfigs WHERE ConsumerTypeCode = '1' AND [Year] = 1405;
    IF @cfgId IS NOT NULL
    BEGIN
        INSERT INTO ConsumerTypeTieredRates (Id, ConsumerTypeYearlyConfigId, TierFrom, TierTo, Coefficient, RatePerKwh, SortOrder) VALUES
            (NEWID(), @cfgId, 0,    100,  0.146, 0, 1),
            (NEWID(), @cfgId, 100,  200,  0.17,  0, 2),
            (NEWID(), @cfgId, 200,  300,  0.365, 0, 3),
            (NEWID(), @cfgId, 300,  500,  2.5,   0, 4),
            (NEWID(), @cfgId, 500,  0,    5,     0, 5);
    END
END");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Seed residential tiered rates"); }

    // Promote existing users to SuperAdmin
    try
    {
        db.Database.ExecuteSqlRaw(@"
UPDATE Users SET Role = 'SuperAdmin' WHERE Username IN ('09167288894', '09166912537') AND Role != 'SuperAdmin'");
        logger.LogInformation("Existing users promoted to SuperAdmin");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Promote users to SuperAdmin failed"); }

    // Create UserCenters junction table (for existing databases)
    try
    {
        db.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('UserCenters') AND type = 'U')
CREATE TABLE UserCenters (
    UserId uniqueidentifier NOT NULL,
    CenterId uniqueidentifier NOT NULL,
    CONSTRAINT PK_UserCenters PRIMARY KEY (UserId, CenterId),
    CONSTRAINT FK_UserCenters_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserCenters_Centers FOREIGN KEY (CenterId) REFERENCES Centers(Id) ON DELETE CASCADE
)");
        logger.LogInformation("UserCenters table OK");
    }
    catch (Exception ex) { logger.LogWarning(ex, "Create UserCenters table failed"); }

    // Comprehensive fix: convert ALL float columns to decimal for Infrastructure DbContext compatibility
    void AlterCol(string table, string col, string type, bool notNull = true)
    {
        var nullable = notNull ? "NOT NULL" : "NULL";
        var sql = $@"
DECLARE @dc NVARCHAR(200);
SELECT @dc = d.name FROM sys.default_constraints d 
JOIN sys.columns c ON c.default_object_id = d.object_id 
WHERE c.name = '{col}' AND OBJECT_NAME(d.parent_object_id) = '{table}';
IF @dc IS NOT NULL EXEC('ALTER TABLE [{table}] DROP CONSTRAINT [' + @dc + ']');
ALTER TABLE [{table}] ALTER COLUMN [{col}] {type} {nullable};";
        try { db.Database.ExecuteSqlRaw(sql); }
        catch (Exception ex) { logger.LogWarning("ALTER failed: {Table}.{Col} => {Msg}", table, col, ex.Message); }
    }
    AlterCol("TariffRates", "RatePerKWh", "decimal(14,2)");
    AlterCol("InvoiceDetails", "KWh", "decimal(14,4)");
    AlterCol("InvoiceDetails", "RatePerKWh", "decimal(14,2)");
    AlterCol("InvoiceDetails", "Amount", "decimal(18,2)");
    AlterCol("InvoiceDetails", "Penalty", "decimal(18,2)", notNull: false);
    AlterCol("TieredRates", "TierFrom", "decimal(18,2)");
    AlterCol("TieredRates", "TierTo", "decimal(18,2)", notNull: false);
    AlterCol("TieredRates", "RatePerKWh", "decimal(18,4)");
    AlterCol("DeviceConfigs", "OverVoltageThreshold", "decimal(10,2)");
    AlterCol("DeviceConfigs", "UnderVoltageThreshold", "decimal(10,2)");
    AlterCol("DeviceConfigs", "OverCurrentThreshold", "decimal(10,2)");
    AlterCol("DeviceConfigs", "PhaseImbalanceThreshold", "decimal(10,2)");
    AlterCol("DeviceConfigs", "LowPFThreshold", "decimal(5,3)");
    AlterCol("DeviceConfigs", "FreqMinThreshold", "decimal(5,2)");
    AlterCol("DeviceConfigs", "FreqMaxThreshold", "decimal(5,2)");
    AlterCol("DeviceConfigs", "HighPowerThreshold", "decimal(12,2)");
    AlterCol("DeviceConfigs", "TemperatureThreshold", "decimal(10,2)");
    AlterCol("AlarmLogs", "Value", "decimal(14,4)", notNull: false);
    AlterCol("EnergyLimits", "MaxValue", "decimal(14,4)");
    AlterCol("EnergyLimits", "AlertThresholdPercent", "decimal(5,2)");
    AlterCol("Tariffs", "OffPeakRate", "decimal(14,4)");
    AlterCol("Tariffs", "MidPeakRate", "decimal(14,4)");
    AlterCol("Tariffs", "PeakRate", "decimal(14,4)");
    AlterCol("Tariffs", "MonthlyFixedFee", "decimal(18,2)");
    AlterCol("Tariffs", "ReactivePenaltyThreshold", "decimal(5,3)");
    AlterCol("Tariffs", "ReactiveBonusThreshold", "decimal(5,3)");
    AlterCol("Tariffs", "ReactivePenaltyMultiplier", "decimal(10,2)");
    AlterCol("Tariffs", "DemandRate", "decimal(14,4)");
    AlterCol("Invoices", "TotalKWh", "decimal(18,4)");
    AlterCol("Invoices", "EnergyCost", "decimal(18,2)");
    AlterCol("Invoices", "MonthlyFixedFeeTotal", "decimal(18,2)");
    AlterCol("Invoices", "ReactivePenalty", "decimal(18,2)");
    AlterCol("Invoices", "SubTotal", "decimal(18,2)");
    AlterCol("Invoices", "TaxAmount", "decimal(18,2)");
    AlterCol("Invoices", "GrandTotal", "decimal(18,2)");
    AlterCol("EnergySnapshots", "VoltageA", "decimal(10,2)");
    AlterCol("EnergySnapshots", "CurrentA", "decimal(10,3)");
    AlterCol("EnergySnapshots", "PowerA", "decimal(14,4)");
    AlterCol("EnergySnapshots", "PfA", "decimal(5,3)");
    AlterCol("EnergySnapshots", "EnergyA", "decimal(18,4)");
    AlterCol("EnergySnapshots", "VoltageB", "decimal(10,2)");
    AlterCol("EnergySnapshots", "CurrentB", "decimal(10,3)");
    AlterCol("EnergySnapshots", "PowerB", "decimal(14,4)");
    AlterCol("EnergySnapshots", "PfB", "decimal(5,3)");
    AlterCol("EnergySnapshots", "EnergyB", "decimal(18,4)");
    AlterCol("EnergySnapshots", "VoltageC", "decimal(10,2)");
    AlterCol("EnergySnapshots", "CurrentC", "decimal(10,3)");
    AlterCol("EnergySnapshots", "PowerC", "decimal(14,4)");
    AlterCol("EnergySnapshots", "PfC", "decimal(5,3)");
    AlterCol("EnergySnapshots", "EnergyC", "decimal(18,4)");
    AlterCol("EnergySnapshots", "Frequency", "decimal(8,3)");
    AlterCol("EnergySnapshots", "Temperature", "decimal(6,2)");
    AlterCol("EnergySnapshots", "TotalPower", "decimal(14,4)");
    AlterCol("EnergyConsumptions", "DeltaA", "decimal(18,4)");
    AlterCol("EnergyConsumptions", "PeakCurrentA", "decimal(10,3)");
    AlterCol("EnergyConsumptions", "PeakPowerA", "decimal(14,4)");
    AlterCol("EnergyConsumptions", "DeltaB", "decimal(18,4)");
    AlterCol("EnergyConsumptions", "PeakCurrentB", "decimal(10,3)");
    AlterCol("EnergyConsumptions", "PeakPowerB", "decimal(14,4)");
    AlterCol("EnergyConsumptions", "DeltaC", "decimal(18,4)");
    AlterCol("EnergyConsumptions", "PeakCurrentC", "decimal(10,3)");
    AlterCol("EnergyConsumptions", "PeakPowerC", "decimal(14,4)");
    logger.LogInformation("Migration: float-to-decimal fix complete");

    // Seed default admin user
    try
    {
        var exists = false;
        try { exists = db.Users.Any(u => u.Username == "09167288894"); } catch { }
        if (!exists)
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var id = Guid.NewGuid();
            var hash = hasher.Hash("2777");
            db.Database.ExecuteSqlRaw(
                "INSERT INTO Users (Id, Username, PasswordHash, FullName, Role, IsActive, CreatedAt) VALUES ({0}, {1}, {2}, {3}, {4}, 1, GETUTCDATE())",
                id, "09167288894", hash, "مدیر سیستم", "SuperAdmin");
            logger.LogInformation("Default admin user seeded: 09167288894");
        }
    }
    catch (Exception ex) { logger.LogWarning(ex, "Seed default user failed"); }

    // Seed admin user: 09166912537 / 25370
    try
    {
        var exists2 = false;
        try { exists2 = db.Users.Any(u => u.Username == "09166912537"); } catch { }
        if (!exists2)
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var id2 = Guid.NewGuid();
            var hash2 = hasher.Hash("25370");
            db.Database.ExecuteSqlRaw(
                "INSERT INTO Users (Id, Username, PasswordHash, FullName, Role, IsActive, CreatedAt) VALUES ({0}, {1}, {2}, {3}, {4}, 1, GETUTCDATE())",
                id2, "09166912537", hash2, "مدیر سیستم", "SuperAdmin");
            logger.LogInformation("Admin user seeded: 09166912537");
        }
    }
    catch (Exception ex) { logger.LogWarning(ex, "Seed admin user 09166912537 failed"); }
}

app.UseResponseCompression();
app.UseCors("All");
app.UseStaticFiles();
app.UseRouting();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseBlazorFrameworkFiles();
app.MapFallbackToFile("index.html");

Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║   Energy Monitor Dashboard                     ║");
Console.WriteLine("║   http://localhost:5204                         ║");
Console.WriteLine("║   Device: POST /api/ingestion/publish            ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");

app.Run();
