IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [Businesses] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Email] nvarchar(256) NULL,
        [Phone] nvarchar(30) NULL,
        [PhysicalAddress] nvarchar(500) NULL,
        [KraPin] nvarchar(20) NULL,
        [LogoPath] nvarchar(500) NULL,
        [TimeZoneId] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Businesses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [SubscriptionPlans] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [UnitLimit] int NULL,
        [MonthlyFee] decimal(18,2) NOT NULL,
        [CreditsPerMonth] int NOT NULL,
        [TrialDays] int NOT NULL,
        [ForBusinessType] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [SystemConfigurations] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(1000) NOT NULL,
        [ValueType] nvarchar(50) NOT NULL,
        [Group] nvarchar(50) NOT NULL,
        [Description] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SystemConfigurations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [BusinessUsers] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Role] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_BusinessUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessUsers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessUsers_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [SubscriptionPayments] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [PlanId] int NOT NULL,
        [Months] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] nvarchar(50) NOT NULL,
        [Reference] nvarchar(50) NOT NULL,
        [ProofStorageKey] nvarchar(300) NULL,
        [ProofFileName] nvarchar(255) NULL,
        [Status] nvarchar(50) NOT NULL,
        [ReviewedByUserId] nvarchar(450) NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [ReviewNote] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SubscriptionPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionPayments_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionPayments_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE TABLE [Subscriptions] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [PlanId] int NOT NULL,
        [PeriodStartUtc] datetime2 NOT NULL,
        [PeriodEndUtc] datetime2 NOT NULL,
        [IsTrial] bit NOT NULL,
        [SourcePaymentId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Subscriptions_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Subscriptions_SubscriptionPayments_SourcePaymentId] FOREIGN KEY ([SourcePaymentId]) REFERENCES [SubscriptionPayments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Subscriptions_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BusinessUsers_BusinessId_UserId] ON [BusinessUsers] ([BusinessId], [UserId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [IX_BusinessUsers_UserId] ON [BusinessUsers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [IX_SubscriptionPayments_BusinessId_Status] ON [SubscriptionPayments] ([BusinessId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SubscriptionPayments_Method_Reference] ON [SubscriptionPayments] ([Method], [Reference]) WHERE [Status] <> ''Rejected'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [IX_SubscriptionPayments_PlanId] ON [SubscriptionPayments] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionPlans_Code] ON [SubscriptionPlans] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_BusinessId_PeriodEndUtc] ON [Subscriptions] ([BusinessId], [PeriodEndUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_PlanId] ON [Subscriptions] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Subscriptions_SourcePaymentId] ON [Subscriptions] ([SourcePaymentId]) WHERE [SourcePaymentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SystemConfigurations_Key] ON [SystemConfigurations] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011114054_Sprint1Subscriptions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011114054_Sprint1Subscriptions', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    CREATE TABLE [Assets] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [LifecycleState] nvarchar(50) NOT NULL,
        [Location] nvarchar(300) NULL,
        [County] nvarchar(50) NULL,
        [LandSizeAcres] decimal(18,4) NULL,
        [AcquisitionCost] decimal(18,2) NULL,
        [AcquisitionDate] date NULL,
        [TitleDeedNumber] nvarchar(100) NULL,
        [Description] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Assets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Assets_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    CREATE TABLE [StructureNodes] (
        [Id] int NOT NULL IDENTITY,
        [AssetId] int NOT NULL,
        [ParentId] int NULL,
        [Name] nvarchar(100) NOT NULL,
        [NodeType] nvarchar(50) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_StructureNodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StructureNodes_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StructureNodes_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StructureNodes_StructureNodes_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [StructureNodes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    CREATE TABLE [Units] (
        [Id] int NOT NULL IDENTITY,
        [AssetId] int NOT NULL,
        [StructureNodeId] int NULL,
        [UnitNumber] nvarchar(30) NOT NULL,
        [FloorNumber] int NULL,
        [Type] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [SizeSqm] decimal(18,2) NULL,
        [Bedrooms] int NULL,
        [Bathrooms] int NULL,
        [MonthlyRent] decimal(18,2) NOT NULL,
        [SalePrice] decimal(18,2) NULL,
        [GracePeriodDays] int NULL,
        [LatePenaltyAmount] decimal(18,2) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Units] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Units_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Units_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Units_StructureNodes_StructureNodeId] FOREIGN KEY ([StructureNodeId]) REFERENCES [StructureNodes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Assets_BusinessId_Name] ON [Assets] ([BusinessId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    CREATE INDEX [IX_StructureNodes_AssetId_ParentId] ON [StructureNodes] ([AssetId], [ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    CREATE INDEX [IX_StructureNodes_BusinessId] ON [StructureNodes] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    CREATE INDEX [IX_StructureNodes_ParentId] ON [StructureNodes] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Units_AssetId_UnitNumber] ON [Units] ([AssetId], [UnitNumber]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    CREATE INDEX [IX_Units_BusinessId_Status] ON [Units] ([BusinessId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    CREATE INDEX [IX_Units_StructureNodeId] ON [Units] ([StructureNodeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011123957_Sprint2Portfolio'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011123957_Sprint2Portfolio', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE TABLE [NumberSequences] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(30) NOT NULL,
        [NextValue] bigint NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_NumberSequences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NumberSequences_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] int NOT NULL IDENTITY,
        [Type] nvarchar(50) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [IdType] nvarchar(50) NULL,
        [IdNumber] nvarchar(50) NULL,
        [KraPin] nvarchar(20) NULL,
        [Phone] nvarchar(20) NULL,
        [AltPhone] nvarchar(20) NULL,
        [Email] nvarchar(256) NULL,
        [PostalAddress] nvarchar(300) NULL,
        [Employer] nvarchar(150) NULL,
        [EmergencyContactName] nvarchar(150) NULL,
        [EmergencyContactPhone] nvarchar(20) NULL,
        [EmergencyContactRelationship] nvarchar(50) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tenants_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE TABLE [Leases] (
        [Id] int NOT NULL IDENTITY,
        [LeaseNumber] nvarchar(20) NOT NULL,
        [TenantId] int NOT NULL,
        [UnitId] int NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [MonthlyRent] decimal(18,2) NOT NULL,
        [DepositAmount] decimal(18,2) NOT NULL,
        [NoticePeriodDays] int NOT NULL,
        [RentDueDay] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [ActivatedAtUtc] datetime2 NULL,
        [EndedOn] date NULL,
        [NoticeGivenOn] date NULL,
        [TerminationReason] nvarchar(500) NULL,
        [TerminationPenalty] decimal(18,2) NULL,
        [DepositDeductions] decimal(18,2) NULL,
        [DepositNote] nvarchar(500) NULL,
        [RenewedFromLeaseId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Leases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Leases_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Leases_Leases_RenewedFromLeaseId] FOREIGN KEY ([RenewedFromLeaseId]) REFERENCES [Leases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Leases_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Leases_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE TABLE [TenantDocuments] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [LeaseId] int NULL,
        [DocumentType] nvarchar(50) NOT NULL,
        [Title] nvarchar(150) NULL,
        [FileName] nvarchar(255) NOT NULL,
        [StorageKey] nvarchar(300) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_TenantDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantDocuments_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantDocuments_Leases_LeaseId] FOREIGN KEY ([LeaseId]) REFERENCES [Leases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Leases_BusinessId_LeaseNumber] ON [Leases] ([BusinessId], [LeaseNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE INDEX [IX_Leases_BusinessId_Status_EndDate] ON [Leases] ([BusinessId], [Status], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Leases_RenewedFromLeaseId] ON [Leases] ([RenewedFromLeaseId]) WHERE [RenewedFromLeaseId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE INDEX [IX_Leases_TenantId] ON [Leases] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Leases_OneActivePerUnit] ON [Leases] ([UnitId]) WHERE [Status] = ''Active'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NumberSequences_BusinessId_Key] ON [NumberSequences] ([BusinessId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE INDEX [IX_TenantDocuments_BusinessId] ON [TenantDocuments] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE INDEX [IX_TenantDocuments_LeaseId] ON [TenantDocuments] ([LeaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE INDEX [IX_TenantDocuments_TenantId] ON [TenantDocuments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE INDEX [IX_Tenants_BusinessId_FullName] ON [Tenants] ([BusinessId], [FullName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Tenants_BusinessId_IdNumber] ON [Tenants] ([BusinessId], [IdNumber]) WHERE [IdNumber] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    CREATE INDEX [IX_Tenants_BusinessId_Phone] ON [Tenants] ([BusinessId], [Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011131325_Sprint3TenantsLeases'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011131325_Sprint3TenantsLeases', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE TABLE [ChargeDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [DefaultAmount] decimal(18,2) NULL,
        [UnitRate] decimal(18,4) NULL,
        [UnitOfMeasure] nvarchar(20) NULL,
        [TaxPercent] decimal(5,2) NOT NULL,
        [Description] nvarchar(300) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_ChargeDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChargeDefinitions_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceNumber] nvarchar(20) NOT NULL,
        [Kind] nvarchar(50) NOT NULL,
        [TenantId] int NOT NULL,
        [LeaseId] int NULL,
        [InvoiceDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [PeriodStart] date NULL,
        [PeriodEnd] date NULL,
        [Status] nvarchar(50) NOT NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [TaxTotal] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [IssuedAtUtc] datetime2 NULL,
        [StatusReason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_Leases_LeaseId] FOREIGN KEY ([LeaseId]) REFERENCES [Leases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE TABLE [LeaseCharges] (
        [Id] int NOT NULL IDENTITY,
        [LeaseId] int NOT NULL,
        [ChargeDefinitionId] int NOT NULL,
        [AmountOverride] decimal(18,2) NULL,
        [UnitRateOverride] decimal(18,4) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_LeaseCharges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeaseCharges_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaseCharges_ChargeDefinitions_ChargeDefinitionId] FOREIGN KEY ([ChargeDefinitionId]) REFERENCES [ChargeDefinitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LeaseCharges_Leases_LeaseId] FOREIGN KEY ([LeaseId]) REFERENCES [Leases] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE TABLE [InvoiceLines] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [LineType] nvarchar(50) NOT NULL,
        [Description] nvarchar(300) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [TaxPercent] decimal(5,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [ChargeDefinitionId] int NULL,
        [MeterReadingId] int NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_InvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoiceLines_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InvoiceLines_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE TABLE [UtilityMeterReadings] (
        [Id] int NOT NULL IDENTITY,
        [UnitId] int NOT NULL,
        [ChargeDefinitionId] int NOT NULL,
        [ReadingDate] date NOT NULL,
        [PreviousReading] decimal(18,3) NOT NULL,
        [CurrentReading] decimal(18,3) NOT NULL,
        [Consumption] decimal(18,3) NOT NULL,
        [UnitRate] decimal(18,4) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [IsOpening] bit NOT NULL,
        [BilledInvoiceId] int NULL,
        [Notes] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_UtilityMeterReadings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UtilityMeterReadings_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UtilityMeterReadings_ChargeDefinitions_ChargeDefinitionId] FOREIGN KEY ([ChargeDefinitionId]) REFERENCES [ChargeDefinitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UtilityMeterReadings_Invoices_BilledInvoiceId] FOREIGN KEY ([BilledInvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UtilityMeterReadings_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChargeDefinitions_BusinessId_Name] ON [ChargeDefinitions] ([BusinessId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_BusinessId] ON [InvoiceLines] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE INDEX [IX_InvoiceLines_InvoiceId] ON [InvoiceLines] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_BusinessId_InvoiceNumber] ON [Invoices] ([BusinessId], [InvoiceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE INDEX [IX_Invoices_BusinessId_Status_DueDate] ON [Invoices] ([BusinessId], [Status], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE INDEX [IX_Invoices_TenantId] ON [Invoices] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Invoices_OneMonthlyPerLeasePeriod] ON [Invoices] ([LeaseId], [PeriodStart]) WHERE [Kind] = ''Monthly'' AND [Status] <> ''Cancelled'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE INDEX [IX_LeaseCharges_BusinessId] ON [LeaseCharges] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE INDEX [IX_LeaseCharges_ChargeDefinitionId] ON [LeaseCharges] ([ChargeDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LeaseCharges_LeaseId_ChargeDefinitionId] ON [LeaseCharges] ([LeaseId], [ChargeDefinitionId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE INDEX [IX_UtilityMeterReadings_BilledInvoiceId] ON [UtilityMeterReadings] ([BilledInvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE INDEX [IX_UtilityMeterReadings_BusinessId_BilledInvoiceId] ON [UtilityMeterReadings] ([BusinessId], [BilledInvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    CREATE INDEX [IX_UtilityMeterReadings_ChargeDefinitionId] ON [UtilityMeterReadings] ([ChargeDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UtilityMeterReadings_UnitId_ChargeDefinitionId_ReadingDate] ON [UtilityMeterReadings] ([UnitId], [ChargeDefinitionId], [ReadingDate]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011134606_Sprint4Billing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011134606_Sprint4Billing', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [ReceiptNumber] nvarchar(20) NOT NULL,
        [TenantId] int NOT NULL,
        [LeaseId] int NULL,
        [PaymentDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] nvarchar(50) NOT NULL,
        [Reference] nvarchar(50) NULL,
        [PayerName] nvarchar(150) NULL,
        [Notes] nvarchar(500) NULL,
        [Status] nvarchar(50) NOT NULL,
        [ReversedAtUtc] datetime2 NULL,
        [ReversedBy] nvarchar(450) NULL,
        [ReversalReason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Leases_LeaseId] FOREIGN KEY ([LeaseId]) REFERENCES [Leases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE TABLE [PaymentAllocations] (
        [Id] int NOT NULL IDENTITY,
        [PaymentId] int NOT NULL,
        [InvoiceId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [IsReversed] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_PaymentAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentAllocations_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentAllocations_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentAllocations_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE TABLE [Refunds] (
        [Id] int NOT NULL IDENTITY,
        [RefundNumber] nvarchar(20) NOT NULL,
        [Kind] nvarchar(50) NOT NULL,
        [TenantId] int NOT NULL,
        [LeaseId] int NULL,
        [PaymentId] int NULL,
        [RefundDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] nvarchar(50) NOT NULL,
        [Reference] nvarchar(50) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Refunds_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Refunds_Leases_LeaseId] FOREIGN KEY ([LeaseId]) REFERENCES [Leases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Refunds_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Refunds_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_BusinessId] ON [PaymentAllocations] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_InvoiceId_IsReversed] ON [PaymentAllocations] ([InvoiceId], [IsReversed]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_PaymentId] ON [PaymentAllocations] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_Payments_BusinessId_PaymentDate] ON [Payments] ([BusinessId], [PaymentDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_BusinessId_ReceiptNumber] ON [Payments] ([BusinessId], [ReceiptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_Payments_BusinessId_TenantId_Status] ON [Payments] ([BusinessId], [TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_Payments_LeaseId] ON [Payments] ([LeaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_Payments_TenantId] ON [Payments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Payments_Reference] ON [Payments] ([BusinessId], [Method], [Reference]) WHERE [Reference] IS NOT NULL AND [Status] <> ''Reversed'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Refunds_BusinessId_RefundNumber] ON [Refunds] ([BusinessId], [RefundNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_Refunds_LeaseId] ON [Refunds] ([LeaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_Refunds_PaymentId] ON [Refunds] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    CREATE INDEX [IX_Refunds_TenantId] ON [Refunds] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011170503_Sprint5Collections'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011170503_Sprint5Collections', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    ALTER TABLE [Tenants] ADD [CollectionStage] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    ALTER TABLE [Invoices] ADD [PenaltyForInvoiceId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    ALTER TABLE [Businesses] ADD [CreditBalance] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    CREATE TABLE [CollectionNotes] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Stage] nvarchar(50) NOT NULL,
        [StageChanged] bit NOT NULL,
        [Note] nvarchar(1000) NOT NULL,
        [DocumentId] int NULL,
        [PromisedPaymentDate] date NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_CollectionNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CollectionNotes_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollectionNotes_TenantDocuments_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [TenantDocuments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollectionNotes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    CREATE TABLE [CreditPurchases] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Credits] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] nvarchar(50) NOT NULL,
        [Reference] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [ReviewedByUserId] nvarchar(450) NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [ReviewNote] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_CreditPurchases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreditPurchases_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    CREATE TABLE [CreditTransactions] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Credits] int NOT NULL,
        [BalanceAfter] int NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [Reference] nvarchar(50) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_CreditTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreditTransactions_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Invoices_OnePenaltyPerInvoice] ON [Invoices] ([PenaltyForInvoiceId]) WHERE [PenaltyForInvoiceId] IS NOT NULL AND [Status] <> ''Cancelled'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    CREATE INDEX [IX_CollectionNotes_BusinessId] ON [CollectionNotes] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    CREATE INDEX [IX_CollectionNotes_DocumentId] ON [CollectionNotes] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    CREATE INDEX [IX_CollectionNotes_TenantId_CreatedAtUtc] ON [CollectionNotes] ([TenantId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    CREATE INDEX [IX_CreditPurchases_BusinessId_Status] ON [CreditPurchases] ([BusinessId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CreditPurchases_Method_Reference] ON [CreditPurchases] ([Method], [Reference]) WHERE [Status] <> ''Rejected'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    CREATE INDEX [IX_CreditTransactions_BusinessId_CreatedAtUtc] ON [CreditTransactions] ([BusinessId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    ALTER TABLE [Invoices] ADD CONSTRAINT [FK_Invoices_Invoices_PenaltyForInvoiceId] FOREIGN KEY ([PenaltyForInvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011172513_Sprint6Arrears'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011172513_Sprint6Arrears', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011180230_Sprint7Accounts'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LastLoginAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011180230_Sprint7Accounts'
)
BEGIN
    CREATE TABLE [BusinessInvitations] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Role] nvarchar(50) NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [AcceptedAtUtc] datetime2 NULL,
        [AcceptedByUserId] nvarchar(450) NULL,
        [RevokedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_BusinessInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessInvitations_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011180230_Sprint7Accounts'
)
BEGIN
    CREATE INDEX [IX_BusinessInvitations_BusinessId_AcceptedAtUtc] ON [BusinessInvitations] ([BusinessId], [AcceptedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011180230_Sprint7Accounts'
)
BEGIN
    CREATE INDEX [IX_BusinessInvitations_Email_AcceptedAtUtc_RevokedAtUtc] ON [BusinessInvitations] ([Email], [AcceptedAtUtc], [RevokedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011180230_Sprint7Accounts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessInvitations_TokenHash] ON [BusinessInvitations] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011180230_Sprint7Accounts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011180230_Sprint7Accounts', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE TABLE [Agents] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(150) NOT NULL,
        [LicenseNumber] nvarchar(50) NULL,
        [Phone] nvarchar(20) NULL,
        [Email] nvarchar(256) NULL,
        [CommissionSharePercent] decimal(5,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Agents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Agents_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE TABLE [PropertyOwners] (
        [Id] int NOT NULL IDENTITY,
        [Type] nvarchar(50) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [IdNumber] nvarchar(50) NULL,
        [KraPin] nvarchar(20) NULL,
        [Phone] nvarchar(20) NULL,
        [Email] nvarchar(256) NULL,
        [PostalAddress] nvarchar(300) NULL,
        [PayoutMethod] nvarchar(50) NOT NULL,
        [BankName] nvarchar(100) NULL,
        [BankAccountName] nvarchar(150) NULL,
        [BankAccountNumber] nvarchar(50) NULL,
        [MpesaNumber] nvarchar(20) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_PropertyOwners] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PropertyOwners_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE TABLE [ManagementContracts] (
        [Id] int NOT NULL IDENTITY,
        [ContractNumber] nvarchar(20) NOT NULL,
        [OwnerId] int NOT NULL,
        [AgentId] int NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [CommissionType] nvarchar(50) NOT NULL,
        [CommissionPercent] decimal(5,2) NOT NULL,
        [FixedMonthlyFee] decimal(18,2) NOT NULL,
        [RemittanceDay] int NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [EndedOn] date NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_ManagementContracts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ManagementContracts_Agents_AgentId] FOREIGN KEY ([AgentId]) REFERENCES [Agents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ManagementContracts_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ManagementContracts_PropertyOwners_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [PropertyOwners] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE TABLE [ContractProperties] (
        [Id] int NOT NULL IDENTITY,
        [ContractId] int NOT NULL,
        [AssetId] int NOT NULL,
        [AddedOn] date NOT NULL,
        [RemovedOn] date NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_ContractProperties] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractProperties_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ContractProperties_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ContractProperties_ManagementContracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [ManagementContracts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE TABLE [OwnerRemittances] (
        [Id] int NOT NULL IDENTITY,
        [RemittanceNumber] nvarchar(20) NOT NULL,
        [ContractId] int NOT NULL,
        [OwnerId] int NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [GrossCollections] decimal(18,2) NOT NULL,
        [DepositsExcluded] decimal(18,2) NOT NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [AdjustmentsTotal] decimal(18,2) NOT NULL,
        [NetPayable] decimal(18,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [PaidOn] date NULL,
        [PaidMethod] nvarchar(50) NULL,
        [PaidReference] nvarchar(50) NULL,
        [StatusReason] nvarchar(500) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_OwnerRemittances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OwnerRemittances_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OwnerRemittances_ManagementContracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [ManagementContracts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OwnerRemittances_PropertyOwners_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [PropertyOwners] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE TABLE [RemittanceLines] (
        [Id] int NOT NULL IDENTITY,
        [RemittanceId] int NOT NULL,
        [LineType] nvarchar(50) NOT NULL,
        [AssetId] int NULL,
        [Description] nvarchar(300) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_RemittanceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RemittanceLines_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RemittanceLines_OwnerRemittances_RemittanceId] FOREIGN KEY ([RemittanceId]) REFERENCES [OwnerRemittances] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_Agents_BusinessId] ON [Agents] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_ContractProperties_BusinessId] ON [ContractProperties] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_ContractProperties_ContractId] ON [ContractProperties] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ContractProperties_OneActivePerAsset] ON [ContractProperties] ([AssetId]) WHERE [IsActive] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_ManagementContracts_AgentId] ON [ManagementContracts] ([AgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ManagementContracts_BusinessId_ContractNumber] ON [ManagementContracts] ([BusinessId], [ContractNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_ManagementContracts_BusinessId_Status] ON [ManagementContracts] ([BusinessId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_ManagementContracts_OwnerId] ON [ManagementContracts] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OwnerRemittances_BusinessId_RemittanceNumber] ON [OwnerRemittances] ([BusinessId], [RemittanceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_OwnerRemittances_OwnerId] ON [OwnerRemittances] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OwnerRemittances_OnePerContractMonth] ON [OwnerRemittances] ([ContractId], [PeriodStart]) WHERE [Status] <> ''Cancelled'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_PropertyOwners_BusinessId_FullName] ON [PropertyOwners] ([BusinessId], [FullName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_RemittanceLines_BusinessId] ON [RemittanceLines] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    CREATE INDEX [IX_RemittanceLines_RemittanceId] ON [RemittanceLines] ([RemittanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012074925_Sprint8Agency'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012074925_Sprint8Agency', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012090607_AgentLogins'
)
BEGIN
    DROP INDEX [IX_Agents_BusinessId] ON [Agents];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012090607_AgentLogins'
)
BEGIN
    ALTER TABLE [Agents] ADD [UserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012090607_AgentLogins'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Agents_BusinessId_UserId] ON [Agents] ([BusinessId], [UserId]) WHERE [UserId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012090607_AgentLogins'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012090607_AgentLogins', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012093047_Sprint9Sales'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012093047_Sprint9Sales', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE TABLE [Buyers] (
        [Id] int NOT NULL IDENTITY,
        [Type] nvarchar(50) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [IdNumber] nvarchar(50) NULL,
        [KraPin] nvarchar(20) NULL,
        [Phone] nvarchar(20) NULL,
        [Email] nvarchar(256) NULL,
        [PostalAddress] nvarchar(300) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Buyers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Buyers_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE TABLE [Sales] (
        [Id] int NOT NULL IDENTITY,
        [SaleNumber] nvarchar(20) NOT NULL,
        [BuyerId] int NOT NULL,
        [UnitId] int NOT NULL,
        [AgentId] int NULL,
        [SaleDate] date NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [DepositAmount] decimal(18,2) NOT NULL,
        [PaymentPlan] nvarchar(50) NOT NULL,
        [InstalmentCount] int NOT NULL,
        [InstalmentFrequency] nvarchar(50) NOT NULL,
        [FirstInstalmentDate] date NULL,
        [CommissionPercent] decimal(5,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [CompletedOn] date NULL,
        [CancelledOn] date NULL,
        [CancelReason] nvarchar(500) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Sales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sales_Agents_AgentId] FOREIGN KEY ([AgentId]) REFERENCES [Agents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sales_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sales_Buyers_BuyerId] FOREIGN KEY ([BuyerId]) REFERENCES [Buyers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Sales_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE TABLE [SaleInstalments] (
        [Id] int NOT NULL IDENTITY,
        [SaleId] int NOT NULL,
        [Number] int NOT NULL,
        [Description] nvarchar(100) NOT NULL,
        [DueDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_SaleInstalments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SaleInstalments_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SaleInstalments_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE TABLE [SaleMilestones] (
        [Id] int NOT NULL IDENTITY,
        [SaleId] int NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [CompletedOn] date NULL,
        [Reference] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [DocumentStorageKey] nvarchar(300) NULL,
        [DocumentFileName] nvarchar(255) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_SaleMilestones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SaleMilestones_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SaleMilestones_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE TABLE [SalePayments] (
        [Id] int NOT NULL IDENTITY,
        [ReceiptNumber] nvarchar(20) NOT NULL,
        [SaleId] int NOT NULL,
        [PaymentDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] nvarchar(50) NOT NULL,
        [Reference] nvarchar(50) NULL,
        [Notes] nvarchar(500) NULL,
        [Status] nvarchar(50) NOT NULL,
        [ReversedAtUtc] datetime2 NULL,
        [ReversedBy] nvarchar(450) NULL,
        [ReversalReason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_SalePayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalePayments_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalePayments_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE TABLE [SaleAllocations] (
        [Id] int NOT NULL IDENTITY,
        [SalePaymentId] int NOT NULL,
        [InstalmentId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [IsReversed] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_SaleAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SaleAllocations_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SaleAllocations_SaleInstalments_InstalmentId] FOREIGN KEY ([InstalmentId]) REFERENCES [SaleInstalments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SaleAllocations_SalePayments_SalePaymentId] FOREIGN KEY ([SalePaymentId]) REFERENCES [SalePayments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_Buyers_BusinessId_FullName] ON [Buyers] ([BusinessId], [FullName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_SaleAllocations_BusinessId] ON [SaleAllocations] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_SaleAllocations_InstalmentId] ON [SaleAllocations] ([InstalmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_SaleAllocations_SalePaymentId] ON [SaleAllocations] ([SalePaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_SaleInstalments_BusinessId_DueDate] ON [SaleInstalments] ([BusinessId], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SaleInstalments_SaleId_Number] ON [SaleInstalments] ([SaleId], [Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_SaleMilestones_BusinessId] ON [SaleMilestones] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SaleMilestones_SaleId_Type] ON [SaleMilestones] ([SaleId], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SalePayments_BusinessId_Method_Reference] ON [SalePayments] ([BusinessId], [Method], [Reference]) WHERE [Reference] IS NOT NULL AND [Status] <> ''Reversed'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_SalePayments_BusinessId_PaymentDate] ON [SalePayments] ([BusinessId], [PaymentDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalePayments_BusinessId_ReceiptNumber] ON [SalePayments] ([BusinessId], [ReceiptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_SalePayments_SaleId] ON [SalePayments] ([SaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_Sales_AgentId] ON [Sales] ([AgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Sales_BusinessId_SaleNumber] ON [Sales] ([BusinessId], [SaleNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_Sales_BusinessId_Status] ON [Sales] ([BusinessId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    CREATE INDEX [IX_Sales_BuyerId] ON [Sales] ([BuyerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Sales_OneLivePerUnit] ON [Sales] ([UnitId]) WHERE [Status] <> ''Cancelled'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012094336_Sprint9SalesTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012094336_Sprint9SalesTables', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    ALTER TABLE [Units] ADD [TitleNumber] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    ALTER TABLE [Assets] ADD [CostAllocation] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    ALTER TABLE [Assets] ADD [EstimatedLandCost] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    ALTER TABLE [Assets] ADD [SellerName] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    ALTER TABLE [Assets] ADD [SubdivisionApprovedOn] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    ALTER TABLE [Assets] ADD [SubdivisionReference] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    CREATE TABLE [ProjectCosts] (
        [Id] int NOT NULL IDENTITY,
        [AssetId] int NOT NULL,
        [UnitId] int NULL,
        [Kind] nvarchar(50) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Date] date NULL,
        [Supplier] nvarchar(150) NULL,
        [Reference] nvarchar(100) NULL,
        [ReceiptStorageKey] nvarchar(300) NULL,
        [ReceiptFileName] nvarchar(255) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_ProjectCosts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectCosts_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectCosts_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectCosts_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    CREATE INDEX [IX_ProjectCosts_AssetId_Kind_Category] ON [ProjectCosts] ([AssetId], [Kind], [Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    CREATE INDEX [IX_ProjectCosts_BusinessId] ON [ProjectCosts] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    CREATE INDEX [IX_ProjectCosts_UnitId] ON [ProjectCosts] ([UnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012113245_DevelopmentCosting'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012113245_DevelopmentCosting', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE TABLE [Listings] (
        [Id] int NOT NULL IDENTITY,
        [UnitId] int NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Title] nvarchar(120) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [PublishedAtUtc] datetime2 NULL,
        [ClosedAtUtc] datetime2 NULL,
        [ViewCount] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_Listings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Listings_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Listings_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE TABLE [ListingLeads] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [ListingId] int NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Message] nvarchar(1000) NULL,
        [ConsentedAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [Stage] nvarchar(50) NOT NULL,
        [VerificationTokenHash] nvarchar(64) NULL,
        [VerifyBeforeUtc] datetime2 NOT NULL,
        [VerifiedAtUtc] datetime2 NULL,
        [IsRepeat] bit NOT NULL,
        [CreditsCharged] int NOT NULL,
        [UnlockedAtUtc] datetime2 NULL,
        [RefundedAtUtc] datetime2 NULL,
        [RefundReason] nvarchar(300) NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_ListingLeads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ListingLeads_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ListingLeads_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE TABLE [ListingPhotos] (
        [Id] int NOT NULL IDENTITY,
        [ListingId] int NOT NULL,
        [StorageKey] nvarchar(300) NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        [BusinessId] int NOT NULL,
        CONSTRAINT [PK_ListingPhotos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ListingPhotos_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ListingPhotos_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [Listings] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE INDEX [IX_ListingLeads_BusinessId_Status_CreatedAtUtc] ON [ListingLeads] ([BusinessId], [Status], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE INDEX [IX_ListingLeads_ListingId_Email] ON [ListingLeads] ([ListingId], [Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE INDEX [IX_ListingLeads_ListingId_Phone] ON [ListingLeads] ([ListingId], [Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ListingLeads_VerificationTokenHash] ON [ListingLeads] ([VerificationTokenHash]) WHERE [VerificationTokenHash] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE INDEX [IX_ListingPhotos_BusinessId] ON [ListingPhotos] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE INDEX [IX_ListingPhotos_ListingId] ON [ListingPhotos] ([ListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE INDEX [IX_Listings_BusinessId] ON [Listings] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    CREATE INDEX [IX_Listings_Status_Type_PublishedAtUtc] ON [Listings] ([Status], [Type], [PublishedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Listings_OneOpenPerUnitType] ON [Listings] ([UnitId], [Type]) WHERE [Status] <> ''Closed'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012155348_Sprint10Marketplace'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012155348_Sprint10Marketplace', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Payments] ADD [ReceiptEmailedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Invoices] ADD [EmailedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [AutoInvoiceDay] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [AutoInvoiceEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [AutoPenaltiesEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [EmailInvoicesToTenants] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [EmailReceiptsToTenants] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [LastAutoInvoiceMonth] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [LastAutomationRunOn] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [LastAutomationSummary] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [PaymentInstructions] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [PenaltyGraceDays] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    ALTER TABLE [Businesses] ADD [PenaltyPercent] decimal(5,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012183011_Sprint11Automation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012183011_Sprint11Automation', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012185116_Sprint11BReportsNotifications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012185116_Sprint11BReportsNotifications', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013102353_RentalToolsSetting'
)
BEGIN
    ALTER TABLE [Businesses] ADD [ShowRentalTools] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013102353_RentalToolsSetting'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013102353_RentalToolsSetting', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013103359_RentalToolsSetting2'
)
BEGIN
    ALTER TABLE [ChargeDefinitions] ADD [AnchorMonth] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013103359_RentalToolsSetting2'
)
BEGIN
    ALTER TABLE [ChargeDefinitions] ADD [Frequency] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013103359_RentalToolsSetting2'
)
BEGIN
    ALTER TABLE [BusinessUsers] ADD [NotificationsSeenAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013103359_RentalToolsSetting2'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [Body] nvarchar(500) NULL,
        [Url] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013103359_RentalToolsSetting2'
)
BEGIN
    CREATE INDEX [IX_Notifications_BusinessId_CreatedAtUtc] ON [Notifications] ([BusinessId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013103359_RentalToolsSetting2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013103359_RentalToolsSetting2', N'10.0.0');
END;

COMMIT;
GO

