CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Allergies" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Type" integer NOT NULL,
    "Allergen" text NOT NULL,
    "Severity" integer NOT NULL,
    "Reaction" text,
    "FirstObserved" timestamp with time zone,
    "Notes" text,
    "RecordedByUserId" uuid NOT NULL,
    "RecordedByName" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Allergies" PRIMARY KEY ("Id")
);

CREATE TABLE "Appointments" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "PatientName" text NOT NULL,
    "PatientPhone" text NOT NULL,
    "DoctorUserId" uuid,
    "DoctorName" text NOT NULL,
    "Department" text NOT NULL,
    "ScheduledAt" timestamp with time zone NOT NULL,
    "DurationMinutes" integer NOT NULL,
    "Status" integer NOT NULL,
    "ChiefComplaint" text,
    "Notes" text,
    "ReminderSent" boolean NOT NULL,
    "FollowUpSent" boolean NOT NULL,
    "CheckedInAt" timestamp with time zone,
    "CompletedAt" timestamp with time zone,
    "ConsultationFee" numeric,
    "IsPaid" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Appointments" PRIMARY KEY ("Id")
);

CREATE TABLE "AuditLogs" (
    "Id" uuid NOT NULL,
    "UserId" uuid,
    "UserName" text,
    "Action" text NOT NULL,
    "EntityType" text,
    "EntityId" text,
    "MetadataJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
);

CREATE TABLE "CampaignRecipients" (
    "Id" uuid NOT NULL,
    "CampaignId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "PatientName" text NOT NULL,
    "PatientPhone" text NOT NULL,
    "Status" integer NOT NULL,
    "WaMessageId" text,
    "ErrorMessage" text,
    "SentAt" timestamp with time zone,
    "RepliedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_CampaignRecipients" PRIMARY KEY ("Id")
);

CREATE TABLE "Campaigns" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "MessageBody" text NOT NULL,
    "AudienceJson" text NOT NULL,
    "Status" integer NOT NULL,
    "ScheduledAt" timestamp with time zone,
    "SentAt" timestamp with time zone,
    "CreatedByUserId" uuid,
    "TotalRecipients" integer NOT NULL,
    "SentCount" integer NOT NULL,
    "DeliveredCount" integer NOT NULL,
    "ReadCount" integer NOT NULL,
    "RepliedCount" integer NOT NULL,
    "FailedCount" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Campaigns" PRIMARY KEY ("Id")
);

CREATE TABLE "ClinicalNotes" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "AppointmentId" uuid,
    "VisitId" uuid,
    "AuthorUserId" uuid NOT NULL,
    "AuthorName" text NOT NULL,
    "NoteType" text NOT NULL,
    "Subjective" text NOT NULL,
    "Objective" text NOT NULL,
    "Assessment" text NOT NULL,
    "Plan" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ClinicalNotes" PRIMARY KEY ("Id")
);

CREATE TABLE "Conversations" (
    "Id" uuid NOT NULL,
    "PatientId" uuid,
    "WaPhone" text NOT NULL,
    "Name" text,
    "Category" integer NOT NULL,
    "Priority" integer NOT NULL,
    "AssignedStaffId" uuid,
    "LastMessageAt" timestamp with time zone NOT NULL,
    "LastMessagePreview" text,
    "UnreadCount" integer NOT NULL,
    "Tags" jsonb NOT NULL,
    "AiSummary" text,
    "AwaitingReplySince" timestamp with time zone,
    "Escalated" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Conversations" PRIMARY KEY ("Id")
);

CREATE TABLE "EmailSettings" (
    "Id" uuid NOT NULL,
    "SmtpHost" text,
    "SmtpPort" integer NOT NULL,
    "SmtpUsername" text,
    "SmtpPasswordEncrypted" text,
    "UseSsl" boolean NOT NULL,
    "FromEmail" text,
    "FromName" text,
    "Enabled" boolean NOT NULL,
    "SendWithWhatsApp" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_EmailSettings" PRIMARY KEY ("Id")
);

CREATE TABLE "FamilyHistory" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Relation" text NOT NULL,
    "Condition" text NOT NULL,
    "AgeOfOnset" integer,
    "Notes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_FamilyHistory" PRIMARY KEY ("Id")
);

CREATE TABLE "HospitalProfiles" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Tagline" text,
    "About" text,
    "Address" text,
    "Phones" jsonb NOT NULL,
    "Emails" jsonb NOT NULL,
    "Website" text,
    "WorkingHours" text,
    "Emergency24x7" boolean NOT NULL,
    "DepartmentsJson" text NOT NULL,
    "ServicesJson" text NOT NULL,
    "DoctorsJson" text NOT NULL,
    "PackagesJson" text NOT NULL,
    "FaqsJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_HospitalProfiles" PRIMARY KEY ("Id")
);

CREATE TABLE "IntegrationEvents" (
    "Id" uuid NOT NULL,
    "EventType" text NOT NULL,
    "EntityType" text,
    "EntityId" text,
    "PayloadJson" text NOT NULL,
    "Processed" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_IntegrationEvents" PRIMARY KEY ("Id")
);

CREATE TABLE "InternalNotes" (
    "Id" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "AuthorId" uuid NOT NULL,
    "AuthorName" text NOT NULL,
    "Body" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_InternalNotes" PRIMARY KEY ("Id")
);

CREATE TABLE "LabReports" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "AppointmentId" uuid,
    "VisitId" uuid,
    "OrderedByUserId" uuid,
    "TestName" text NOT NULL,
    "Category" text,
    "LabName" text,
    "SampleCollectedAt" timestamp with time zone,
    "ReportedAt" timestamp with time zone,
    "Results" text,
    "Interpretation" text,
    "FileUrl" text,
    "IsAbnormal" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_LabReports" PRIMARY KEY ("Id")
);

CREATE TABLE "LifestyleProfiles" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "AverageSleepHours" numeric,
    "SleepQuality" text,
    "WakeUpTime" interval,
    "BedTime" interval,
    "WaterIntakeLitersPerDay" numeric,
    "MealsPerDay" integer,
    "MealTimings" text,
    "SkipsBreakfast" boolean NOT NULL,
    "DietType" text,
    "CuisinePreferences" text,
    "FoodAllergiesText" text,
    "DietaryRestrictions" text,
    "CaffeineCupsPerDay" numeric,
    "ConsumesProcessedFood" boolean NOT NULL,
    "ConsumesSugaryDrinks" boolean NOT NULL,
    "ExercisesRegularly" boolean NOT NULL,
    "ExerciseMinutesPerWeek" integer,
    "ExerciseType" text,
    "PhysicalActivityLevel" text,
    "SmokingStatus" integer NOT NULL,
    "CigarettesPerDay" integer,
    "YearsOfSmoking" integer,
    "AlcoholConsumption" integer NOT NULL,
    "AlcoholDetails" text,
    "ChewsTobaccoOrPaan" boolean NOT NULL,
    "OtherSubstances" text,
    "Occupation" text,
    "WorkSchedule" text,
    "WorkHoursPerDay" integer,
    "HighStressJob" boolean NOT NULL,
    "StressLevel" text,
    "StressManagementMethods" text,
    "HobbiesAndInterests" text,
    "MentalHealthConcerns" text,
    "HasAnxietyOrDepression" boolean,
    "CurrentMentalHealthTreatment" text,
    "MenstrualCycleStatus" text,
    "LastMenstrualPeriod" text,
    "NumberOfPregnancies" integer,
    "ContraceptiveUse" text,
    "LivingEnvironment" text,
    "ExposureToPollution" boolean NOT NULL,
    "PetExposure" text,
    "AdditionalLifestyleNotes" text,
    "LastUpdatedAt" timestamp with time zone NOT NULL,
    "LastUpdatedByUserId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_LifestyleProfiles" PRIMARY KEY ("Id")
);

CREATE TABLE "MarketingCalendarEvents" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "EventDate" date NOT NULL,
    "Category" text NOT NULL,
    "Region" text NOT NULL,
    "Source" text NOT NULL,
    "SuggestedMessage" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_MarketingCalendarEvents" PRIMARY KEY ("Id")
);

CREATE TABLE "MedicalHistory" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Category" text NOT NULL,
    "Title" text NOT NULL,
    "OnsetDate" timestamp with time zone,
    "IsOngoing" boolean NOT NULL,
    "Description" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_MedicalHistory" PRIMARY KEY ("Id")
);

CREATE TABLE "Messages" (
    "Id" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "PatientId" uuid,
    "WaMessageId" text,
    "Direction" integer NOT NULL,
    "Type" text NOT NULL,
    "Body" text NOT NULL,
    "MediaUrl" text,
    "MimeType" text,
    "Caption" text,
    "Status" integer NOT NULL,
    "SenderUserId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Messages" PRIMARY KEY ("Id")
);

CREATE TABLE "Patients" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Phone" text NOT NULL,
    "Email" text,
    "EmailNotificationsEnabled" boolean NOT NULL,
    "Age" integer,
    "DateOfBirth" timestamp with time zone,
    "Gender" integer NOT NULL,
    "BloodGroup" integer NOT NULL,
    "AddressLine1" text,
    "AddressLine2" text,
    "City" text,
    "State" text,
    "Pincode" text,
    "EmergencyContactName" text,
    "EmergencyContactPhone" text,
    "EmergencyContactRelation" text,
    "Department" text,
    "InquirySource" text NOT NULL,
    "ReferralDoctor" text,
    "ReferringDoctorId" uuid,
    "AssignedStaffId" uuid,
    "Status" integer NOT NULL,
    "Tags" jsonb NOT NULL,
    "Notes" text,
    "LastContactAt" timestamp with time zone,
    "FollowUpDate" timestamp with time zone,
    "AiSummary" text,
    "AiLeadScore" integer,
    "GovIdType" text,
    "GovIdNumber" text,
    "Occupation" text,
    "MaritalStatus" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Patients" PRIMARY KEY ("Id")
);

CREATE TABLE "PermissionGroups" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PermissionGroups" PRIMARY KEY ("Id")
);

CREATE TABLE "Prescriptions" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "DoctorUserId" uuid NOT NULL,
    "DoctorName" text NOT NULL,
    "AppointmentId" uuid,
    "VisitId" uuid,
    "PrescribedAt" timestamp with time zone NOT NULL,
    "Diagnosis" text,
    "ChiefComplaint" text,
    "ClinicalNotes" text,
    "FollowUpAdvice" text,
    "NextVisitDate" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Prescriptions" PRIMARY KEY ("Id")
);

CREATE TABLE "Referrals" (
    "Id" uuid NOT NULL,
    "DoctorId" uuid NOT NULL,
    "DoctorName" text NOT NULL,
    "PatientId" uuid NOT NULL,
    "PatientName" text NOT NULL,
    "Revenue" numeric NOT NULL,
    "Notes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Referrals" PRIMARY KEY ("Id")
);

CREATE TABLE "ReferringDoctors" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Clinic" text,
    "Specialty" text,
    "Phone" text,
    "Email" text,
    "Category" integer NOT NULL,
    "Tags" jsonb NOT NULL,
    "Notes" text,
    "ReconnectEveryDays" integer NOT NULL,
    "LastContactAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ReferringDoctors" PRIMARY KEY ("Id")
);

CREATE TABLE "Roles" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "IsSystem" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
);

CREATE TABLE "SmsSettings" (
    "Id" uuid NOT NULL,
    "GatewayUrl" text,
    "ApiKeyEncrypted" text,
    "SenderId" text,
    "Enabled" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_SmsSettings" PRIMARY KEY ("Id")
);

CREATE TABLE "Tasks" (
    "Id" uuid NOT NULL,
    "Title" text NOT NULL,
    "Type" integer NOT NULL,
    "PatientId" uuid,
    "PatientName" text,
    "ConversationId" uuid,
    "AssignedTo" uuid,
    "Status" integer NOT NULL,
    "Priority" integer NOT NULL,
    "DueAt" timestamp with time zone,
    "Notes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Tasks" PRIMARY KEY ("Id")
);

CREATE TABLE "TemplatePlaceholders" (
    "Id" uuid NOT NULL,
    "Key" text NOT NULL,
    "Label" text NOT NULL,
    "Description" text,
    "Example" text,
    "IsSystem" boolean NOT NULL,
    "StaticValue" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_TemplatePlaceholders" PRIMARY KEY ("Id")
);

CREATE TABLE "Templates" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Body" text NOT NULL,
    "Category" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Templates" PRIMARY KEY ("Id")
);

CREATE TABLE "Tenants" (
    "Id" uuid NOT NULL,
    "Slug" text NOT NULL,
    "Name" text NOT NULL,
    "ContactName" text,
    "ContactEmail" text,
    "ContactPhone" text,
    "Country" text,
    "Timezone" text,
    "Plan" integer NOT NULL,
    "SubscriptionStatus" integer NOT NULL,
    "TrialEndsAt" timestamp with time zone,
    "SubscriptionRenewsAt" timestamp with time zone,
    "SeatLimit" integer NOT NULL,
    "PatientLimit" integer NOT NULL,
    "MessagesQuotaMonthly" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_Tenants" PRIMARY KEY ("Id")
);

CREATE TABLE "Visits" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "AppointmentId" uuid,
    "DoctorUserId" uuid NOT NULL,
    "DoctorName" text NOT NULL,
    "Department" text,
    "VisitDate" timestamp with time zone NOT NULL,
    "EndedAt" timestamp with time zone,
    "Status" integer NOT NULL,
    "VisitType" text NOT NULL,
    "ChiefComplaint" text,
    "Symptoms" text,
    "Diagnosis" text,
    "DoctorNotes" text,
    "FollowUpAdvice" text,
    "FollowUpDate" timestamp with time zone,
    "AttachmentsJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Visits" PRIMARY KEY ("Id")
);

CREATE TABLE "VitalSigns" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "AppointmentId" uuid,
    "VisitId" uuid,
    "RecordedByUserId" uuid NOT NULL,
    "RecordedByName" text,
    "MeasuredAt" timestamp with time zone NOT NULL,
    "HeightCm" numeric,
    "WeightKg" numeric,
    "Bmi" numeric,
    "SystolicBp" integer,
    "DiastolicBp" integer,
    "HeartRate" integer,
    "Temperature" numeric,
    "RespiratoryRate" integer,
    "OxygenSaturation" integer,
    "BloodSugarFasting" numeric,
    "BloodSugarPostprandial" numeric,
    "Hba1c" numeric,
    "Notes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_VitalSigns" PRIMARY KEY ("Id")
);

CREATE TABLE "WhatsAppCampaigns" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Type" text NOT NULL,
    "Status" text NOT NULL,
    "ExternalId" integer NOT NULL,
    "ExternalDeletedAt" timestamp with time zone,
    "ExternalCreatedAt" timestamp with time zone NOT NULL,
    "ExternalUpdatedAt" timestamp with time zone NOT NULL,
    "CachedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_WhatsAppCampaigns" PRIMARY KEY ("Id")
);

CREATE TABLE "WhatsAppGroups" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "ExternalId" integer NOT NULL,
    "ExternalDeletedAt" timestamp with time zone,
    "ExternalCreatedAt" timestamp with time zone NOT NULL,
    "ExternalUpdatedAt" timestamp with time zone NOT NULL,
    "CachedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_WhatsAppGroups" PRIMARY KEY ("Id")
);

CREATE TABLE "WhatsAppMessages" (
    "Id" uuid NOT NULL,
    "Phone" text NOT NULL,
    "MessageType" text NOT NULL,
    "Content" text NOT NULL,
    "Status" text NOT NULL,
    "ExternalMessageId" text,
    "ErrorMessage" text,
    "SentAt" timestamp with time zone NOT NULL,
    "DeliveredAt" timestamp with time zone,
    "ReadAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_WhatsAppMessages" PRIMARY KEY ("Id")
);

CREATE TABLE "WhatsAppSettings" (
    "Id" uuid NOT NULL,
    "Provider" text NOT NULL,
    "PhoneNumberId" text,
    "WabaId" text,
    "AccessTokenEncrypted" text,
    "ApiTokenEncrypted" text,
    "WhatsBizBaseUrl" text,
    "VerifyToken" text,
    "AppSecretEncrypted" text,
    "BusinessName" text NOT NULL,
    "Enabled" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_WhatsAppSettings" PRIMARY KEY ("Id")
);

CREATE TABLE "WhatsAppTemplates" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Status" text NOT NULL,
    "Category" text NOT NULL,
    "Language" text NOT NULL,
    "Components" text NOT NULL,
    "ExternalId" integer NOT NULL,
    "ExternalCreatedAt" timestamp with time zone NOT NULL,
    "ExternalUpdatedAt" timestamp with time zone NOT NULL,
    "CachedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_WhatsAppTemplates" PRIMARY KEY ("Id")
);

CREATE TABLE "EmailMessages" (
    "Id" uuid NOT NULL,
    "PatientId" uuid,
    "ToEmail" text NOT NULL,
    "Subject" text,
    "Body" text NOT NULL,
    "Direction" integer NOT NULL,
    "Status" integer NOT NULL,
    "ErrorMessage" text,
    "SentAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_EmailMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EmailMessages_Patients_PatientId" FOREIGN KEY ("PatientId") REFERENCES "Patients" ("Id")
);

CREATE TABLE "Permissions" (
    "Id" uuid NOT NULL,
    "Code" text NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "PermissionGroupId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_Permissions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Permissions_PermissionGroups_PermissionGroupId" FOREIGN KEY ("PermissionGroupId") REFERENCES "PermissionGroups" ("Id") ON DELETE SET NULL
);

CREATE TABLE "Injections" (
    "Id" uuid NOT NULL,
    "PrescriptionId" uuid NOT NULL,
    "Name" text NOT NULL,
    "GenericName" text,
    "Strength" text,
    "Site" text,
    "Route" integer NOT NULL,
    "AdministeredAt" timestamp with time zone NOT NULL,
    "AdministeredByUserId" uuid,
    "AdministeredBy" text NOT NULL,
    "BatchNumber" text,
    "ExpiryDate" timestamp with time zone,
    "ReasonForInjection" text NOT NULL,
    "AdverseReaction" text,
    "Notes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Injections" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Injections_Prescriptions_PrescriptionId" FOREIGN KEY ("PrescriptionId") REFERENCES "Prescriptions" ("Id") ON DELETE CASCADE
);

CREATE TABLE "PrescriptionItems" (
    "Id" uuid NOT NULL,
    "PrescriptionId" uuid NOT NULL,
    "DrugName" text NOT NULL,
    "GenericName" text,
    "Strength" text,
    "Form" text,
    "Route" integer NOT NULL,
    "Dosage" text,
    "Frequency" text,
    "Duration" text,
    "Timing" text,
    "Quantity" integer,
    "ReasonForPrescribing" text NOT NULL,
    "PossibleSideEffects" text,
    "PatientInstructions" text,
    "IsContinuation" boolean NOT NULL,
    "IsAcute" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PrescriptionItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PrescriptionItems_Prescriptions_PrescriptionId" FOREIGN KEY ("PrescriptionId") REFERENCES "Prescriptions" ("Id") ON DELETE CASCADE
);

CREATE TABLE "UserRoleAssignments" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_UserRoleAssignments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_UserRoleAssignments_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Email" text NOT NULL,
    "PasswordHash" text NOT NULL,
    "Role" integer NOT NULL,
    "Specialty" text,
    "Qualifications" text,
    "RegistrationNumber" text,
    "Phone" text,
    "ProfileImageUrl" text,
    "Bio" text,
    "LastLoginAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Users_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
);

CREATE TABLE "WhatsAppContacts" (
    "Id" uuid NOT NULL,
    "Phone" text NOT NULL,
    "Name" text NOT NULL,
    "ExternalGroupId" character varying(64) NOT NULL,
    "WhatsAppGroupId" uuid,
    "CustomFields" text,
    "ExternalId" text NOT NULL,
    "ExternalCreatedAt" timestamp with time zone NOT NULL,
    "ExternalUpdatedAt" timestamp with time zone NOT NULL,
    "CachedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_WhatsAppContacts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_WhatsAppContacts_WhatsAppGroups_WhatsAppGroupId" FOREIGN KEY ("WhatsAppGroupId") REFERENCES "WhatsAppGroups" ("Id") ON DELETE SET NULL
);

CREATE TABLE "RolePermissions" (
    "Id" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "PermissionId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RolePermissions_Permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES "Permissions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_RolePermissions_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "StaffProfiles" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Department" text,
    "Specialization" text,
    "Qualification" text,
    "ConsultationFee" numeric,
    "EmploymentType" integer NOT NULL,
    "Shift" text,
    "WardAssignment" text,
    "IsAvailable" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_StaffProfiles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_StaffProfiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "DoctorSchedules" (
    "Id" uuid NOT NULL,
    "StaffProfileId" uuid NOT NULL,
    "DayOfWeek" integer,
    "SpecificDate" timestamp with time zone,
    "StartTime" interval NOT NULL,
    "EndTime" interval NOT NULL,
    "IsAvailable" boolean NOT NULL,
    "Notes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_DoctorSchedules" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DoctorSchedules_StaffProfiles_StaffProfileId" FOREIGN KEY ("StaffProfileId") REFERENCES "StaffProfiles" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Allergies_TenantId_PatientId" ON "Allergies" ("TenantId", "PatientId");

CREATE INDEX "IX_Appointments_TenantId_PatientId" ON "Appointments" ("TenantId", "PatientId");

CREATE INDEX "IX_Appointments_TenantId_ScheduledAt" ON "Appointments" ("TenantId", "ScheduledAt");

CREATE INDEX "IX_Appointments_TenantId_Status_ScheduledAt" ON "Appointments" ("TenantId", "Status", "ScheduledAt");

CREATE INDEX "IX_Campaigns_TenantId_Status_ScheduledAt" ON "Campaigns" ("TenantId", "Status", "ScheduledAt");

CREATE INDEX "IX_ClinicalNotes_TenantId_PatientId" ON "ClinicalNotes" ("TenantId", "PatientId");

CREATE INDEX "IX_Conversations_TenantId_PatientId" ON "Conversations" ("TenantId", "PatientId");

CREATE INDEX "IX_Conversations_TenantId_WaPhone" ON "Conversations" ("TenantId", "WaPhone");

CREATE INDEX "IX_DoctorSchedules_StaffProfileId" ON "DoctorSchedules" ("StaffProfileId");

CREATE INDEX "IX_EmailMessages_PatientId" ON "EmailMessages" ("PatientId");

CREATE UNIQUE INDEX "IX_HospitalProfiles_TenantId" ON "HospitalProfiles" ("TenantId");

CREATE INDEX "IX_Injections_PrescriptionId" ON "Injections" ("PrescriptionId");

CREATE UNIQUE INDEX "IX_LifestyleProfiles_TenantId_PatientId" ON "LifestyleProfiles" ("TenantId", "PatientId");

CREATE INDEX "IX_MarketingCalendarEvents_TenantId_EventDate" ON "MarketingCalendarEvents" ("TenantId", "EventDate");

CREATE INDEX "IX_Messages_ConversationId" ON "Messages" ("ConversationId");

CREATE INDEX "IX_Messages_TenantId_ConversationId" ON "Messages" ("TenantId", "ConversationId");

CREATE INDEX "IX_Messages_WaMessageId" ON "Messages" ("WaMessageId");

CREATE INDEX "IX_Patients_TenantId_CreatedAt" ON "Patients" ("TenantId", "CreatedAt");

CREATE INDEX "IX_Patients_TenantId_Phone" ON "Patients" ("TenantId", "Phone");

CREATE INDEX "IX_Patients_TenantId_Status" ON "Patients" ("TenantId", "Status");

CREATE UNIQUE INDEX "IX_Permissions_Code" ON "Permissions" ("Code");

CREATE INDEX "IX_Permissions_PermissionGroupId" ON "Permissions" ("PermissionGroupId");

CREATE INDEX "IX_PrescriptionItems_PrescriptionId" ON "PrescriptionItems" ("PrescriptionId");

CREATE INDEX "IX_Prescriptions_TenantId_PatientId" ON "Prescriptions" ("TenantId", "PatientId");

CREATE INDEX "IX_RolePermissions_PermissionId" ON "RolePermissions" ("PermissionId");

CREATE UNIQUE INDEX "IX_RolePermissions_RoleId_PermissionId" ON "RolePermissions" ("RoleId", "PermissionId");

CREATE UNIQUE INDEX "IX_Roles_Name" ON "Roles" ("Name");

CREATE UNIQUE INDEX "IX_StaffProfiles_TenantId_UserId" ON "StaffProfiles" ("TenantId", "UserId");

CREATE INDEX "IX_StaffProfiles_UserId" ON "StaffProfiles" ("UserId");

CREATE UNIQUE INDEX "IX_TemplatePlaceholders_TenantId_Key" ON "TemplatePlaceholders" ("TenantId", "Key");

CREATE UNIQUE INDEX "IX_Tenants_Slug" ON "Tenants" ("Slug");

CREATE INDEX "IX_UserRoleAssignments_RoleId" ON "UserRoleAssignments" ("RoleId");

CREATE INDEX "IX_UserRoleAssignments_TenantId_UserId" ON "UserRoleAssignments" ("TenantId", "UserId");

CREATE UNIQUE INDEX "IX_Users_TenantId_Email" ON "Users" ("TenantId", "Email");

CREATE INDEX "IX_Visits_TenantId_DoctorUserId_VisitDate" ON "Visits" ("TenantId", "DoctorUserId", "VisitDate");

CREATE INDEX "IX_Visits_TenantId_PatientId" ON "Visits" ("TenantId", "PatientId");

CREATE INDEX "IX_Visits_TenantId_Status_VisitDate" ON "Visits" ("TenantId", "Status", "VisitDate");

CREATE INDEX "IX_VitalSigns_TenantId_PatientId" ON "VitalSigns" ("TenantId", "PatientId");

CREATE INDEX "IX_WhatsAppContacts_WhatsAppGroupId" ON "WhatsAppContacts" ("WhatsAppGroupId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260531184938_InitialCreate', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Messages" ADD "FileName" text;

ALTER TABLE "Messages" ADD "MediaSize" bigint;

ALTER TABLE "Messages" ADD "MediaStoredPath" text;

ALTER TABLE "Messages" ADD "ThumbnailUrl" text;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260606042527_WhatsAppMedia', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "NotificationPreferences" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "NotificationType" text NOT NULL,
    "Channel" integer NOT NULL,
    "Enabled" boolean NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_NotificationPreferences" PRIMARY KEY ("Id")
);

CREATE TABLE "Notifications" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Type" text NOT NULL,
    "Title" text NOT NULL,
    "Body" text,
    "Severity" integer NOT NULL,
    "EntityType" text,
    "EntityId" uuid,
    "ActionUrl" text,
    "IsRead" boolean NOT NULL,
    "ReadAt" timestamp with time zone,
    "DedupeKey" text,
    "MetadataJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id")
);

CREATE TABLE "RoleNotificationDefaults" (
    "Id" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    "NotificationType" text NOT NULL,
    "Channel" integer NOT NULL,
    "Enabled" boolean NOT NULL,
    "IsSystem" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_RoleNotificationDefaults" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_NotificationPreferences_TenantId_UserId_NotificationType_Ch~" ON "NotificationPreferences" ("TenantId", "UserId", "NotificationType", "Channel");

CREATE UNIQUE INDEX "IX_Notifications_TenantId_UserId_DedupeKey" ON "Notifications" ("TenantId", "UserId", "DedupeKey") WHERE "DedupeKey" IS NOT NULL;

CREATE INDEX "IX_Notifications_TenantId_UserId_IsRead_CreatedAt" ON "Notifications" ("TenantId", "UserId", "IsRead", "CreatedAt");

CREATE UNIQUE INDEX "IX_RoleNotificationDefaults_RoleId_NotificationType_Channel" ON "RoleNotificationDefaults" ("RoleId", "NotificationType", "Channel");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260628082046_AddNotifications', '10.0.0');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_NotificationPreferences_TenantId_UserId_NotificationType_Ch~";

CREATE UNIQUE INDEX "IX_NotificationPreferences_TenantId_UserId_NotificationType_Ch~" ON "NotificationPreferences" ("TenantId", "UserId", "NotificationType", "Channel") WHERE "IsDeleted" = false;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260628083514_FixNotificationPreferenceUniqueIndex', '10.0.0');

COMMIT;

START TRANSACTION;
DROP TABLE "Notifications";

CREATE TABLE "NotificationEvents" (
    "Id" uuid NOT NULL,
    "Type" text NOT NULL,
    "Title" text NOT NULL,
    "Body" text,
    "Severity" integer NOT NULL,
    "EntityType" text,
    "EntityId" uuid,
    "ActionUrl" text,
    "DedupeKey" text,
    "MetadataJson" text NOT NULL,
    "CreatedByUserId" uuid,
    "AudienceMode" integer NOT NULL,
    "TargetUserIdsJson" text,
    "RequiredPermissionsCsv" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_NotificationEvents" PRIMARY KEY ("Id")
);

CREATE TABLE "NotificationFeedCursors" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "LastReadAllAt" timestamp with time zone,
    "FeedSince" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_NotificationFeedCursors" PRIMARY KEY ("Id")
);

CREATE TABLE "NotificationReceipts" (
    "Id" uuid NOT NULL,
    "NotificationEventId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ReadAt" timestamp with time zone,
    "DismissedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_NotificationReceipts" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_NotificationEvents_TenantId_CreatedAt" ON "NotificationEvents" ("TenantId", "CreatedAt");

CREATE INDEX "IX_NotificationEvents_TenantId_Type" ON "NotificationEvents" ("TenantId", "Type");

CREATE UNIQUE INDEX "IX_NotificationEvents_TenantId_Type_DedupeKey" ON "NotificationEvents" ("TenantId", "Type", "DedupeKey") WHERE "DedupeKey" IS NOT NULL;

CREATE UNIQUE INDEX "IX_NotificationFeedCursors_TenantId_UserId" ON "NotificationFeedCursors" ("TenantId", "UserId") WHERE "IsDeleted" = false;

CREATE UNIQUE INDEX "IX_NotificationReceipts_TenantId_UserId_NotificationEventId" ON "NotificationReceipts" ("TenantId", "UserId", "NotificationEventId") WHERE "IsDeleted" = false;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260628101739_NotificationFeedModel', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Users" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Users" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Users";

CREATE POLICY tenant_isolation ON "Users"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Patients" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Patients" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Patients";

CREATE POLICY tenant_isolation ON "Patients"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Conversations" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Conversations" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Conversations";

CREATE POLICY tenant_isolation ON "Conversations"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Messages" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Messages" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Messages";

CREATE POLICY tenant_isolation ON "Messages"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "InternalNotes" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "InternalNotes" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "InternalNotes";

CREATE POLICY tenant_isolation ON "InternalNotes"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Appointments" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Appointments" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Appointments";

CREATE POLICY tenant_isolation ON "Appointments"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Tasks" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Tasks" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Tasks";

CREATE POLICY tenant_isolation ON "Tasks"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "ReferringDoctors" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ReferringDoctors" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "ReferringDoctors";

CREATE POLICY tenant_isolation ON "ReferringDoctors"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Referrals" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Referrals" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Referrals";

CREATE POLICY tenant_isolation ON "Referrals"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Campaigns" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Campaigns" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Campaigns";

CREATE POLICY tenant_isolation ON "Campaigns"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "CampaignRecipients" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "CampaignRecipients" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "CampaignRecipients";

CREATE POLICY tenant_isolation ON "CampaignRecipients"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "MarketingCalendarEvents" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "MarketingCalendarEvents" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "MarketingCalendarEvents";

CREATE POLICY tenant_isolation ON "MarketingCalendarEvents"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Allergies" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Allergies" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Allergies";

CREATE POLICY tenant_isolation ON "Allergies"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Prescriptions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Prescriptions" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Prescriptions";

CREATE POLICY tenant_isolation ON "Prescriptions"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "PrescriptionItems" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PrescriptionItems" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "PrescriptionItems";

CREATE POLICY tenant_isolation ON "PrescriptionItems"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Injections" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Injections" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Injections";

CREATE POLICY tenant_isolation ON "Injections"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "VitalSigns" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "VitalSigns" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "VitalSigns";

CREATE POLICY tenant_isolation ON "VitalSigns"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "LabReports" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "LabReports" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "LabReports";

CREATE POLICY tenant_isolation ON "LabReports"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "ClinicalNotes" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ClinicalNotes" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "ClinicalNotes";

CREATE POLICY tenant_isolation ON "ClinicalNotes"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "MedicalHistory" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "MedicalHistory" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "MedicalHistory";

CREATE POLICY tenant_isolation ON "MedicalHistory"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "FamilyHistory" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "FamilyHistory" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "FamilyHistory";

CREATE POLICY tenant_isolation ON "FamilyHistory"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Visits" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Visits" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Visits";

CREATE POLICY tenant_isolation ON "Visits"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "StaffProfiles" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "StaffProfiles" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "StaffProfiles";

CREATE POLICY tenant_isolation ON "StaffProfiles"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "DoctorSchedules" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "DoctorSchedules" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "DoctorSchedules";

CREATE POLICY tenant_isolation ON "DoctorSchedules"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "LifestyleProfiles" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "LifestyleProfiles" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "LifestyleProfiles";

CREATE POLICY tenant_isolation ON "LifestyleProfiles"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "HospitalProfiles" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "HospitalProfiles" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "HospitalProfiles";

CREATE POLICY tenant_isolation ON "HospitalProfiles"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "Templates" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Templates" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "Templates";

CREATE POLICY tenant_isolation ON "Templates"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "TemplatePlaceholders" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "TemplatePlaceholders" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "TemplatePlaceholders";

CREATE POLICY tenant_isolation ON "TemplatePlaceholders"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "AuditLogs" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AuditLogs" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "AuditLogs";

CREATE POLICY tenant_isolation ON "AuditLogs"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "WhatsAppSettings" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "WhatsAppSettings" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "WhatsAppSettings";

CREATE POLICY tenant_isolation ON "WhatsAppSettings"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "SmsSettings" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "SmsSettings" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "SmsSettings";

CREATE POLICY tenant_isolation ON "SmsSettings"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "EmailSettings" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "EmailSettings" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "EmailSettings";

CREATE POLICY tenant_isolation ON "EmailSettings"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "EmailMessages" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "EmailMessages" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "EmailMessages";

CREATE POLICY tenant_isolation ON "EmailMessages"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "IntegrationEvents" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "IntegrationEvents" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "IntegrationEvents";

CREATE POLICY tenant_isolation ON "IntegrationEvents"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "UserRoleAssignments" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "UserRoleAssignments" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "UserRoleAssignments";

CREATE POLICY tenant_isolation ON "UserRoleAssignments"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "NotificationEvents" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "NotificationEvents" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "NotificationEvents";

CREATE POLICY tenant_isolation ON "NotificationEvents"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "NotificationReceipts" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "NotificationReceipts" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "NotificationReceipts";

CREATE POLICY tenant_isolation ON "NotificationReceipts"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "NotificationFeedCursors" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "NotificationFeedCursors" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "NotificationFeedCursors";

CREATE POLICY tenant_isolation ON "NotificationFeedCursors"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

ALTER TABLE "NotificationPreferences" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "NotificationPreferences" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "NotificationPreferences";

CREATE POLICY tenant_isolation ON "NotificationPreferences"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260706124111_EnableTenantRowLevelSecurity', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Tenants" ADD "ApprovedAt" timestamp with time zone;

ALTER TABLE "Tenants" ADD "ApprovedByPlatformUserId" uuid;

ALTER TABLE "Tenants" ADD "LifecycleStatus" integer NOT NULL DEFAULT 0;

ALTER TABLE "Tenants" ADD "OnboardingComplete" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Tenants" ADD "RejectionReason" text;

CREATE TABLE "PlatformAuditLogs" (
    "Id" uuid NOT NULL,
    "PlatformUserId" uuid,
    "TenantId" uuid,
    "Action" text NOT NULL,
    "MetadataJson" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PlatformAuditLogs" PRIMARY KEY ("Id")
);

CREATE TABLE "PlatformUsers" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Email" text NOT NULL,
    "PasswordHash" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PlatformUsers" PRIMARY KEY ("Id")
);

CREATE TABLE "TenantOnboardingStates" (
    "Id" uuid NOT NULL,
    "ProfileComplete" boolean NOT NULL,
    "WhatsAppConnected" boolean NOT NULL,
    "TeamInvited" boolean NOT NULL,
    "CompletedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_TenantOnboardingStates" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_Tenants_LifecycleStatus" ON "Tenants" ("LifecycleStatus");

CREATE UNIQUE INDEX "IX_PlatformUsers_Email" ON "PlatformUsers" ("Email");

CREATE UNIQUE INDEX "IX_TenantOnboardingStates_TenantId" ON "TenantOnboardingStates" ("TenantId");

UPDATE "Tenants"
SET "LifecycleStatus" = 1,
    "OnboardingComplete" = true,
    "ApprovedAt" = COALESCE("ApprovedAt", NOW())
WHERE "IsDeleted" = false;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260706174704_TenantLifecycleAndPlatformOps', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "PatientDocuments" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "VisitId" uuid,
    "AppointmentId" uuid,
    "DocumentType" text NOT NULL,
    "Title" text NOT NULL,
    "OriginalFileName" text NOT NULL,
    "ContentType" text NOT NULL,
    "FileSizeBytes" bigint NOT NULL,
    "FileUrl" text NOT NULL,
    "UploadedByUserId" uuid,
    "UploadedByName" text,
    "CapturedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientDocuments" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_PatientDocuments_PatientId_CapturedAt" ON "PatientDocuments" ("PatientId", "CapturedAt");

CREATE INDEX "IX_PatientDocuments_VisitId" ON "PatientDocuments" ("VisitId");

ALTER TABLE "PatientDocuments" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientDocuments" FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON "PatientDocuments";

CREATE POLICY tenant_isolation ON "PatientDocuments"
  USING (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  )
  WITH CHECK (
    current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
  );

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260719073754_AddPatientDocuments', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Patients" ADD "SourceLeadId" uuid;

ALTER TABLE "CampaignRecipients" ALTER COLUMN "PatientId" DROP NOT NULL;

ALTER TABLE "CampaignRecipients" ADD "LeadId" uuid;

CREATE TABLE "Leads" (
    "Id" uuid NOT NULL,
    "Phone" character varying(15) NOT NULL,
    "Name" character varying(200),
    "Age" integer,
    "Gender" integer NOT NULL,
    "ConvertedPatientId" uuid,
    "ConvertedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_Leads" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_Patients_SourceLeadId" ON "Patients" ("SourceLeadId") WHERE "SourceLeadId" IS NOT NULL;

CREATE INDEX "IX_CampaignRecipients_LeadId" ON "CampaignRecipients" ("LeadId");

CREATE INDEX "IX_Leads_TenantId_ConvertedAt" ON "Leads" ("TenantId", "ConvertedAt");

CREATE UNIQUE INDEX "IX_Leads_TenantId_Phone" ON "Leads" ("TenantId", "Phone") WHERE "IsDeleted" = false;

ALTER TABLE "Leads" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Leads" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON "Leads"
USING (current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
WITH CHECK (current_setting('app.platform_admin', true) = 'true'
    OR "TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260915181417_AddLeadsAndCampaignRecipients', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "RefreshSessions" (
    "TokenHash" text NOT NULL,
    "FamilyId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "Consumed" boolean NOT NULL,
    "Revoked" boolean NOT NULL,
    CONSTRAINT "PK_RefreshSessions" PRIMARY KEY ("TokenHash")
);

CREATE INDEX "IX_RefreshSessions_FamilyId" ON "RefreshSessions" ("FamilyId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260916052220_AddRefreshSessions', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "ClinicalNotes" ADD "EditableUntil" timestamp with time zone;

ALTER TABLE "ClinicalNotes" ADD "FinalizedAt" timestamp with time zone;

ALTER TABLE "ClinicalNotes" ADD "LastAutoSavedAt" timestamp with time zone;

ALTER TABLE "ClinicalNotes" ADD "OriginalLanguage" text NOT NULL DEFAULT '';

ALTER TABLE "ClinicalNotes" ADD "Revision" integer NOT NULL DEFAULT 0;

DO $backfill$
DECLARE previous_scope text := current_setting('app.platform_admin', true);
BEGIN
    PERFORM set_config('app.platform_admin', 'true', true);
    UPDATE "ClinicalNotes" SET "OriginalLanguage" = 'und', "Revision" = 1,
        "FinalizedAt" = "CreatedAt",
        "EditableUntil" = LEAST("CreatedAt" + INTERVAL '24 hours', CURRENT_TIMESTAMP);
    PERFORM set_config('app.platform_admin', COALESCE(previous_scope, 'false'), true);
END;
$backfill$;

CREATE FUNCTION protect_doctor_note() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD."FinalizedAt" IS NOT NULL THEN
            RAISE EXCEPTION 'Finalized Doctor Notes cannot be deleted';
        END IF;
        RETURN OLD;
    END IF;
    IF NEW."PatientId" IS DISTINCT FROM OLD."PatientId"
        OR NEW."VisitId" IS DISTINCT FROM OLD."VisitId"
        OR NEW."AppointmentId" IS DISTINCT FROM OLD."AppointmentId"
        OR NEW."AuthorUserId" IS DISTINCT FROM OLD."AuthorUserId"
        OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId"
        OR NEW."AuthorName" IS DISTINCT FROM OLD."AuthorName" THEN
        RAISE EXCEPTION 'Doctor Note association and authorship are immutable';
    END IF;
    IF OLD."FinalizedAt" IS NOT NULL THEN
        IF NEW."IsDeleted" OR NOT NEW."IsActive"
            OR NEW."FinalizedAt" IS DISTINCT FROM OLD."FinalizedAt"
            OR NEW."EditableUntil" IS DISTINCT FROM OLD."EditableUntil" THEN
            RAISE EXCEPTION 'Finalized Doctor Notes cannot be removed or reopened';
        END IF;
        IF OLD."EditableUntil" IS NULL OR clock_timestamp() >= OLD."EditableUntil" THEN
            RAISE EXCEPTION 'Doctor Note editing window has ended';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER protect_doctor_note BEFORE UPDATE OR DELETE ON "ClinicalNotes"
    FOR EACH ROW EXECUTE FUNCTION protect_doctor_note();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260920172752_DoctorNoteLifecycle', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "Facilities" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Code" text,
    "Address" text,
    "City" text,
    "Country" text,
    "IsPrimary" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_Facilities" PRIMARY KEY ("Id")
);

CREATE TABLE "PlatformModules" (
    "Id" uuid NOT NULL,
    "Key" text NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "IsCore" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PlatformModules" PRIMARY KEY ("Id")
);

CREATE TABLE "PlatformOperationJobs" (
    "Id" uuid NOT NULL,
    "JobType" text NOT NULL,
    "TenantId" uuid,
    "Status" text NOT NULL,
    "PayloadJson" text,
    "Error" text,
    "StartedAt" timestamp with time zone,
    "CompletedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PlatformOperationJobs" PRIMARY KEY ("Id")
);

CREATE TABLE "PlatformSupportSessions" (
    "Id" uuid NOT NULL,
    "PlatformUserId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Reason" text NOT NULL,
    "Scope" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PlatformSupportSessions" PRIMARY KEY ("Id")
);

CREATE TABLE "TenantEntitlements" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PlatformModuleId" uuid NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "Limit" integer,
    "ExpiresAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_TenantEntitlements" PRIMARY KEY ("Id")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260926194657_PlatformControlPlaneModelSync', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "TenantType" integer NOT NULL DEFAULT 0;

ALTER TABLE "TenantEntitlements" ADD "Version" integer NOT NULL DEFAULT 0;

ALTER TABLE "PlatformUsers" ADD "Role" text NOT NULL DEFAULT 'owner';

CREATE TABLE "PlatformCommands" (
    "Id" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "RequestId" text NOT NULL,
    "Fingerprint" text NOT NULL,
    "ResponseJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PlatformCommands" PRIMARY KEY ("Id")
);

CREATE TABLE "PlatformRoles" (
    "Id" uuid NOT NULL,
    "Key" text NOT NULL,
    "Name" text NOT NULL,
    "PermissionsJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PlatformRoles" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_TenantEntitlements_PlatformModuleId" ON "TenantEntitlements" ("PlatformModuleId");

CREATE UNIQUE INDEX "IX_TenantEntitlements_TenantId_PlatformModuleId" ON "TenantEntitlements" ("TenantId", "PlatformModuleId");

CREATE INDEX "IX_PlatformSupportSessions_PlatformUserId" ON "PlatformSupportSessions" ("PlatformUserId");

CREATE INDEX "IX_PlatformSupportSessions_TenantId" ON "PlatformSupportSessions" ("TenantId");

CREATE UNIQUE INDEX "IX_PlatformModules_Key" ON "PlatformModules" ("Key");

CREATE UNIQUE INDEX "IX_Facilities_TenantId" ON "Facilities" ("TenantId") WHERE "IsPrimary" = true AND "IsDeleted" = false;

CREATE UNIQUE INDEX "IX_PlatformCommands_ActorId_RequestId" ON "PlatformCommands" ("ActorId", "RequestId");

CREATE UNIQUE INDEX "IX_PlatformRoles_Key" ON "PlatformRoles" ("Key");

ALTER TABLE "Facilities" ADD CONSTRAINT "FK_Facilities_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT;

ALTER TABLE "PlatformSupportSessions" ADD CONSTRAINT "FK_PlatformSupportSessions_PlatformUsers_PlatformUserId" FOREIGN KEY ("PlatformUserId") REFERENCES "PlatformUsers" ("Id") ON DELETE RESTRICT;

ALTER TABLE "PlatformSupportSessions" ADD CONSTRAINT "FK_PlatformSupportSessions_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT;

ALTER TABLE "TenantEntitlements" ADD CONSTRAINT "FK_TenantEntitlements_PlatformModules_PlatformModuleId" FOREIGN KEY ("PlatformModuleId") REFERENCES "PlatformModules" ("Id") ON DELETE RESTRICT;

ALTER TABLE "TenantEntitlements" ADD CONSTRAINT "FK_TenantEntitlements_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260926200813_CompletePlatformAdministration', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "PlanModules" (
    "Id" uuid NOT NULL,
    "Plan" integer NOT NULL,
    "PlatformModuleId" uuid NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PlanModules" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PlanModules_PlatformModules_PlatformModuleId" FOREIGN KEY ("PlatformModuleId") REFERENCES "PlatformModules" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "TenantApiActivities" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ActorId" uuid,
    "Method" text NOT NULL,
    "Route" text NOT NULL,
    "StatusCode" integer NOT NULL,
    "DurationMs" bigint NOT NULL,
    "CorrelationId" text NOT NULL,
    "ErrorCode" text,
    "ErrorSummary" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_TenantApiActivities" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_PlanModules_Plan_PlatformModuleId" ON "PlanModules" ("Plan", "PlatformModuleId");

CREATE INDEX "IX_PlanModules_PlatformModuleId" ON "PlanModules" ("PlatformModuleId");

CREATE INDEX "IX_TenantApiActivities_StatusCode_CreatedAt" ON "TenantApiActivities" ("StatusCode", "CreatedAt");

CREATE INDEX "IX_TenantApiActivities_TenantId_CreatedAt" ON "TenantApiActivities" ("TenantId", "CreatedAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260927142920_SyncSnapshot', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "PlatformSessions" (
    "Id" uuid NOT NULL,
    "PlatformUserId" uuid NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    "SecurityRevision" integer NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_PlatformSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PlatformSessions_PlatformUsers_PlatformUserId" FOREIGN KEY ("PlatformUserId") REFERENCES "PlatformUsers" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_PlatformSessions_PlatformUserId_ExpiresAt" ON "PlatformSessions" ("PlatformUserId", "ExpiresAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006074400_AddPlatformSessions', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "PatientDocuments" ADD "Sha256" text;

ALTER TABLE "PatientDocuments" ADD "StorageKey" text;

ALTER TABLE "PatientDocuments" ADD "StorageVersion" uuid;

ALTER TABLE "PatientDocuments" ADD "StoredLength" bigint;

ALTER TABLE "LabReports" ADD "OriginalFileName" text;

ALTER TABLE "LabReports" ADD "Sha256" text;

ALTER TABLE "LabReports" ADD "StorageKey" text;

ALTER TABLE "LabReports" ADD "StorageVersion" uuid;

ALTER TABLE "LabReports" ADD "StoredContentType" text;

ALTER TABLE "LabReports" ADD "StoredLength" bigint;

CREATE TABLE "PrivateDocumentVersions" (
    "Id" uuid NOT NULL,
    "DocumentId" uuid NOT NULL,
    "DocumentKind" character varying(32) NOT NULL,
    "ObjectKey" character varying(65) NOT NULL,
    "Sha256" character varying(64) NOT NULL,
    "Length" bigint NOT NULL,
    "ContentType" character varying(128) NOT NULL,
    "LegacyFileUrl" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PrivateDocumentVersions" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_PatientDocuments_StorageVersion" ON "PatientDocuments" ("StorageVersion");

CREATE INDEX "IX_LabReports_StorageVersion" ON "LabReports" ("StorageVersion");

CREATE UNIQUE INDEX "IX_PrivateDocumentVersions_TenantId_ObjectKey" ON "PrivateDocumentVersions" ("TenantId", "ObjectKey");

ALTER TABLE "LabReports" ADD CONSTRAINT "FK_LabReports_PrivateDocumentVersions_StorageVersion" FOREIGN KEY ("StorageVersion") REFERENCES "PrivateDocumentVersions" ("Id") ON DELETE RESTRICT;

ALTER TABLE "PatientDocuments" ADD CONSTRAINT "FK_PatientDocuments_PrivateDocumentVersions_StorageVersion" FOREIGN KEY ("StorageVersion") REFERENCES "PrivateDocumentVersions" ("Id") ON DELETE RESTRICT;

ALTER TABLE "PrivateDocumentVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PrivateDocumentVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY private_document_version_tenant ON "PrivateDocumentVersions"
    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE FUNCTION prevent_private_document_version_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Retained document versions are immutable' USING ERRCODE='23514'; END $$;
CREATE TRIGGER private_document_versions_append_only BEFORE UPDATE OR DELETE ON "PrivateDocumentVersions"
    FOR EACH ROW EXECUTE FUNCTION prevent_private_document_version_rewrite();
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
        GRANT SELECT,INSERT ON "PrivateDocumentVersions" TO cureflow_tenant;
        REVOKE UPDATE,DELETE ON "PrivateDocumentVersions" FROM cureflow_tenant;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006080323_AddPrivateDocumentVersions', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "ClinicalAuditEvents" (
    "Id" uuid NOT NULL,
    "ActorKind" character varying(32) NOT NULL,
    "ActorId" uuid,
    "ServiceIdentity" character varying(64),
    "PatientId" uuid,
    "ResourceType" character varying(64) NOT NULL,
    "ResourceId" uuid,
    "ResourceVersion" text,
    "Action" character varying(64) NOT NULL,
    "Purpose" character varying(64) NOT NULL,
    "Outcome" character varying(32) NOT NULL,
    "CorrelationId" character varying(128) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ClinicalAuditEvents" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_ClinicalAuditEvents_TenantId_CreatedAt" ON "ClinicalAuditEvents" ("TenantId", "CreatedAt");

ALTER TABLE "ClinicalAuditEvents" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ClinicalAuditEvents" FORCE ROW LEVEL SECURITY;
CREATE POLICY clinical_audit_tenant ON "ClinicalAuditEvents"
    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE FUNCTION prevent_clinical_audit_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Clinical audit evidence is append-only' USING ERRCODE='23514'; END $$;
CREATE TRIGGER clinical_audit_append_only BEFORE UPDATE OR DELETE ON "ClinicalAuditEvents"
    FOR EACH ROW EXECUTE FUNCTION prevent_clinical_audit_rewrite();
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
        GRANT INSERT ON "ClinicalAuditEvents" TO cureflow_tenant;
        REVOKE SELECT,UPDATE,DELETE ON "ClinicalAuditEvents" FROM cureflow_tenant;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006081412_AddClinicalAuditEvents', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "IdempotencyRecords" (
    "Id" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "CommandNamespace" character varying(96) NOT NULL,
    "KeyHash" character varying(64) NOT NULL,
    "RequestHash" character varying(64) NOT NULL,
    "State" character varying(32) NOT NULL,
    "ResponseJson" character varying(1024) NOT NULL,
    "ResponseStatus" integer NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_IdempotencyRecords" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_IdempotencyRecords_TenantId_ActorId_CommandNamespace_KeyHash" ON "IdempotencyRecords" ("TenantId", "ActorId", "CommandNamespace", "KeyHash");

ALTER TABLE "IdempotencyRecords" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "IdempotencyRecords" FORCE ROW LEVEL SECURITY;
CREATE POLICY idempotent_command_tenant ON "IdempotencyRecords"
  USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
    GRANT SELECT,INSERT,UPDATE ON "IdempotencyRecords" TO cureflow_tenant;
    REVOKE DELETE ON "IdempotencyRecords" FROM cureflow_tenant;
  END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006083201_AddIdempotencyRecords', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "OutboxEvents" (
    "Id" uuid NOT NULL,
    "EventType" character varying(96) NOT NULL,
    "SchemaVersion" integer NOT NULL,
    "AggregateType" character varying(64) NOT NULL,
    "AggregateId" uuid NOT NULL,
    "AggregateVersion" character varying(128) NOT NULL,
    "PayloadJson" text NOT NULL,
    "Attempts" integer NOT NULL,
    "DueAt" timestamp with time zone NOT NULL,
    "State" character varying(32) NOT NULL,
    "LeaseOwner" uuid,
    "LeaseToken" uuid,
    "LeaseUntil" timestamp with time zone,
    "CompletedAt" timestamp with time zone,
    "FailureCode" character varying(64),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_OutboxEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OutboxEvents_TenantId_Id" UNIQUE ("TenantId", "Id")
);

CREATE TABLE "OutboxConsumerReceipts" (
    "Id" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "ConsumerName" character varying(48) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_OutboxConsumerReceipts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OutboxConsumerReceipts_OutboxEvents_TenantId_EventId" FOREIGN KEY ("TenantId", "EventId") REFERENCES "OutboxEvents" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_OutboxConsumerReceipts_TenantId_EventId_ConsumerName" ON "OutboxConsumerReceipts" ("TenantId", "EventId", "ConsumerName");

CREATE INDEX "IX_OutboxEvents_TenantId_State_DueAt_LeaseUntil" ON "OutboxEvents" ("TenantId", "State", "DueAt", "LeaseUntil");

ALTER TABLE "OutboxEvents" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "OutboxEvents" FORCE ROW LEVEL SECURITY;
ALTER TABLE "OutboxConsumerReceipts" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "OutboxConsumerReceipts" FORCE ROW LEVEL SECURITY;
CREATE POLICY outbox_tenant ON "OutboxEvents"
    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY outbox_receipt_tenant ON "OutboxConsumerReceipts"
    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "OutboxEvents" ADD CONSTRAINT outbox_payload_size CHECK (octet_length("PayloadJson")<=65536),
    ADD CONSTRAINT outbox_state CHECK ("State" IN ('pending','leased','completed','failed')),
    ADD CONSTRAINT outbox_attempts CHECK ("Attempts">=0 AND "SchemaVersion">0);
CREATE FUNCTION prevent_outbox_provenance_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Outbox provenance must be retained' USING ERRCODE='23514'; END IF;
    IF ROW(OLD."Id",OLD."TenantId",OLD."EventType",OLD."SchemaVersion",OLD."AggregateType",OLD."AggregateId",OLD."AggregateVersion",OLD."PayloadJson",OLD."CreatedAt",OLD."CreatedBy")
       IS DISTINCT FROM ROW(NEW."Id",NEW."TenantId",NEW."EventType",NEW."SchemaVersion",NEW."AggregateType",NEW."AggregateId",NEW."AggregateVersion",NEW."PayloadJson",NEW."CreatedAt",NEW."CreatedBy")
    THEN RAISE EXCEPTION 'Outbox provenance is immutable' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER outbox_provenance BEFORE UPDATE OR DELETE ON "OutboxEvents"
    FOR EACH ROW EXECUTE FUNCTION prevent_outbox_provenance_rewrite();
CREATE FUNCTION prevent_outbox_receipt_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Outbox receipts are append-only' USING ERRCODE='23514'; END $$;
CREATE TRIGGER outbox_receipt_append_only BEFORE UPDATE OR DELETE ON "OutboxConsumerReceipts"
    FOR EACH ROW EXECUTE FUNCTION prevent_outbox_receipt_rewrite();
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
        GRANT SELECT,INSERT,UPDATE ON "OutboxEvents" TO cureflow_tenant;
        REVOKE DELETE ON "OutboxEvents" FROM cureflow_tenant;
        GRANT SELECT,INSERT ON "OutboxConsumerReceipts" TO cureflow_tenant;
        REVOKE UPDATE,DELETE ON "OutboxConsumerReceipts" FROM cureflow_tenant;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006084226_AddDurableOutbox', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "PatientDocuments" ADD "ScanEvidenceId" uuid;

ALTER TABLE "PatientDocuments" ADD "ScanStatus" character varying(32) NOT NULL DEFAULT 'unresolved';

ALTER TABLE "PatientDocuments" ADD "ScannedAt" timestamp with time zone;

CREATE TABLE "DocumentScanEvidence" (
    "Id" uuid NOT NULL,
    "DocumentId" uuid NOT NULL,
    "Version" uuid NOT NULL,
    "Sha256" character varying(64) NOT NULL,
    "Verdict" character varying(32) NOT NULL,
    "Engine" character varying(64) NOT NULL,
    "EngineVersion" character varying(64) NOT NULL,
    "Code" character varying(64) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_DocumentScanEvidence" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_PatientDocuments_ScanEvidenceId" ON "PatientDocuments" ("ScanEvidenceId");

CREATE INDEX "IX_PatientDocuments_TenantId_ScanStatus" ON "PatientDocuments" ("TenantId", "ScanStatus");

CREATE UNIQUE INDEX "IX_DocumentScanEvidence_TenantId_DocumentId_Version" ON "DocumentScanEvidence" ("TenantId", "DocumentId", "Version");

ALTER TABLE "PatientDocuments" ADD CONSTRAINT "FK_PatientDocuments_DocumentScanEvidence_ScanEvidenceId" FOREIGN KEY ("ScanEvidenceId") REFERENCES "DocumentScanEvidence" ("Id") ON DELETE RESTRICT;

ALTER TABLE "DocumentScanEvidence" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "DocumentScanEvidence" FORCE ROW LEVEL SECURITY;
CREATE POLICY document_scan_tenant ON "DocumentScanEvidence"
    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientDocuments" ADD CONSTRAINT document_scan_status CHECK ("ScanStatus" IN ('unresolved','staged','quarantined','available','rejected','failed'));
CREATE FUNCTION prevent_document_scan_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Document scan evidence is append-only' USING ERRCODE='23514'; END $$;
CREATE TRIGGER document_scan_append_only BEFORE UPDATE OR DELETE ON "DocumentScanEvidence"
    FOR EACH ROW EXECUTE FUNCTION prevent_document_scan_rewrite();
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
        GRANT SELECT,INSERT ON "DocumentScanEvidence" TO cureflow_tenant;
        REVOKE UPDATE,DELETE ON "DocumentScanEvidence" FROM cureflow_tenant;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006084738_AddDocumentQuarantine', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Users" ADD "SecurityRevision" integer NOT NULL DEFAULT 1;

ALTER TABLE "RefreshSessions" ADD "SecurityRevision" integer NOT NULL DEFAULT 0;

CREATE INDEX "IX_RefreshSessions_FamilyId_UserId_SecurityRevision" ON "RefreshSessions" ("FamilyId", "UserId", "SecurityRevision");

CREATE FUNCTION enforce_user_security_revision() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
BEGIN
    IF ROW(OLD."PasswordHash",OLD."Role",OLD."IsActive",OLD."IsDeleted",OLD."TenantId")
       IS DISTINCT FROM ROW(NEW."PasswordHash",NEW."Role",NEW."IsActive",NEW."IsDeleted",NEW."TenantId")
       OR NEW."SecurityRevision">OLD."SecurityRevision"
    THEN
        NEW."SecurityRevision":=GREATEST(OLD."SecurityRevision"+1,NEW."SecurityRevision");
        UPDATE public."RefreshSessions" SET "Revoked"=true WHERE "UserId"=OLD."Id";
    ELSE NEW."SecurityRevision":=OLD."SecurityRevision";
    END IF;
    RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION enforce_user_security_revision() FROM PUBLIC;
CREATE TRIGGER user_security_revision BEFORE UPDATE ON "Users"
    FOR EACH ROW EXECUTE FUNCTION enforce_user_security_revision();
CREATE FUNCTION revoke_role_assignment_sessions() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
BEGIN
    IF TG_OP <> 'INSERT' THEN
        UPDATE public."Users" SET "SecurityRevision"="SecurityRevision"+1 WHERE "Id"=OLD."UserId" AND "TenantId"=OLD."TenantId";
    END IF;
    IF TG_OP <> 'DELETE' AND (TG_OP='INSERT' OR ROW(NEW."UserId",NEW."TenantId") IS DISTINCT FROM ROW(OLD."UserId",OLD."TenantId")) THEN
        UPDATE public."Users" SET "SecurityRevision"="SecurityRevision"+1 WHERE "Id"=NEW."UserId" AND "TenantId"=NEW."TenantId";
    END IF;
    RETURN NULL;
END $$;
REVOKE ALL ON FUNCTION revoke_role_assignment_sessions() FROM PUBLIC;
CREATE TRIGGER role_assignment_sessions AFTER INSERT OR UPDATE OR DELETE ON "UserRoleAssignments"
    FOR EACH ROW EXECUTE FUNCTION revoke_role_assignment_sessions();
CREATE FUNCTION revoke_role_permission_sessions() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
BEGIN
    IF TG_OP <> 'INSERT' THEN
        UPDATE public."Users" u SET "SecurityRevision"=u."SecurityRevision"+1 WHERE EXISTS
         (SELECT 1 FROM public."UserRoleAssignments" a WHERE a."RoleId"=OLD."RoleId" AND a."UserId"=u."Id" AND a."TenantId"=u."TenantId" AND NOT a."IsDeleted");
    END IF;
    IF TG_OP <> 'DELETE' AND (TG_OP='INSERT' OR NEW."RoleId" IS DISTINCT FROM OLD."RoleId") THEN
        UPDATE public."Users" u SET "SecurityRevision"=u."SecurityRevision"+1 WHERE EXISTS
         (SELECT 1 FROM public."UserRoleAssignments" a WHERE a."RoleId"=NEW."RoleId" AND a."UserId"=u."Id" AND a."TenantId"=u."TenantId" AND NOT a."IsDeleted");
    END IF;
    RETURN NULL;
END $$;
REVOKE ALL ON FUNCTION revoke_role_permission_sessions() FROM PUBLIC;
CREATE TRIGGER role_permission_sessions AFTER INSERT OR UPDATE OR DELETE ON "RolePermissions"
    FOR EACH ROW EXECUTE FUNCTION revoke_role_permission_sessions();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006085211_BindTenantSessionAuthority', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "OutboxEvents" ADD "OperatorRetries" integer NOT NULL DEFAULT 0;

ALTER TABLE "OutboxEvents" ADD "Revision" bigint NOT NULL DEFAULT 1;

ALTER TABLE "OutboxEvents" ADD CONSTRAINT outbox_operator_budget CHECK ("OperatorRetries" BETWEEN 0 AND 3 AND "Revision">0);
CREATE FUNCTION enforce_outbox_retry_version() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW."Attempts"<OLD."Attempts" OR NEW."OperatorRetries"<OLD."OperatorRetries"
       OR NEW."OperatorRetries">OLD."OperatorRetries"+1 THEN
        RAISE EXCEPTION 'Outbox retry provenance cannot be reset' USING ERRCODE='23514';
    END IF;
    IF NEW."OperatorRetries">OLD."OperatorRetries" AND
       (OLD."State"<>'failed' OR NEW."State"<>'pending' OR OLD."LeaseOwner" IS NOT NULL OR OLD."LeaseToken" IS NOT NULL OR OLD."LeaseUntil" IS NOT NULL)
    THEN RAISE EXCEPTION 'Outbox manual retry requires failed unleased work' USING ERRCODE='23514'; END IF;
    NEW."Revision":=OLD."Revision"+1;
    RETURN NEW;
END $$;
CREATE TRIGGER outbox_retry_version BEFORE UPDATE ON "OutboxEvents"
    FOR EACH ROW EXECUTE FUNCTION enforce_outbox_retry_version();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
    VALUES ('8c78f486-827b-4ba6-845c-8587b723aa37','Job.View','Inspect failed jobs','Restricted technical summaries only',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
           ('4244f084-45d6-4510-8581-e236b7834385','Job.Retry','Retry failed jobs','Bounded immutable version-safe retry',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
    ON CONFLICT ("Code") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007094631_AddJobRetryControl', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "TenantProcessingPolicies" (
    "Id" uuid NOT NULL,
    "Provider" character varying(64) NOT NULL,
    "Purpose" character varying(64) NOT NULL,
    "Revision" integer NOT NULL,
    "Allowed" boolean NOT NULL,
    "ActorKind" character varying(16) NOT NULL,
    "ApprovalReference" character varying(128) NOT NULL,
    "BasisReference" character varying(128) NOT NULL,
    "BasisRevision" integer NOT NULL,
    "ApprovedBy" uuid NOT NULL,
    "ApprovedAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_TenantProcessingPolicies" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_TenantProcessingPolicies_TenantId_Provider_Purpose_Revision" ON "TenantProcessingPolicies" ("TenantId", "Provider", "Purpose", "Revision");

ALTER TABLE "TenantProcessingPolicies" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "TenantProcessingPolicies" FORCE ROW LEVEL SECURITY;
CREATE POLICY processing_policy_tenant ON "TenantProcessingPolicies"
    USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
    WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "TenantProcessingPolicies" ADD CONSTRAINT processing_policy_evidence
    CHECK ("Revision">0 AND "BasisRevision">0 AND "ActorKind" IN ('person','service')
        AND "ExpiresAt">"ApprovedAt" AND length("ApprovalReference")>0 AND length("BasisReference")>0);
CREATE FUNCTION prevent_processing_policy_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Processing approval history is immutable; append a superseding decision' USING ERRCODE='23514'; END $$;
CREATE TRIGGER processing_policy_append_only BEFORE UPDATE OR DELETE ON "TenantProcessingPolicies"
    FOR EACH ROW EXECUTE FUNCTION prevent_processing_policy_rewrite();
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
        GRANT SELECT ON "TenantProcessingPolicies" TO cureflow_tenant;
        REVOKE INSERT,UPDATE,DELETE ON "TenantProcessingPolicies" FROM cureflow_tenant;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007095142_AddTenantProcessingPolicies', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Tenants" ADD "ReviewStartedAt" timestamp with time zone;

ALTER TABLE "Tenants" ADD "ReviewStartedByPlatformUserId" uuid;

ALTER TABLE "Tenants" ADD "ReviewedAt" timestamp with time zone;

ALTER TABLE "Tenants" ADD "ReviewedByPlatformUserId" uuid;

ALTER TABLE "Tenants" ADD "SubmittedAt" timestamp with time zone;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007125053_AddTenantLifecycleReviewEvidence', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Facilities" ADD CONSTRAINT "AK_Facilities_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "FacilityLocations" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ParentId" uuid,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(64) NOT NULL,
    "Kind" character varying(32) NOT NULL,
    "Revision" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_FacilityLocations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_FacilityLocations_TenantId_FacilityId_Id" UNIQUE ("TenantId", "FacilityId", "Id"),
    CONSTRAINT "FK_FacilityLocations_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_FacilityLocations_FacilityLocations_TenantId_FacilityId_Par~" FOREIGN KEY ("TenantId", "FacilityId", "ParentId") REFERENCES "FacilityLocations" ("TenantId", "FacilityId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "FacilityOperatingSettings" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "Revision" bigint NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "Timezone" character varying(100) NOT NULL,
    "Language" character varying(35) NOT NULL,
    "DateFormat" character varying(20) NOT NULL,
    "TimeFormat" character varying(20) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_FacilityOperatingSettings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_FacilityOperatingSettings_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "OrganizationUnits" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ParentId" uuid,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(64) NOT NULL,
    "Kind" character varying(32) NOT NULL,
    "Revision" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_OrganizationUnits" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_OrganizationUnits_TenantId_FacilityId_Id" UNIQUE ("TenantId", "FacilityId", "Id"),
    CONSTRAINT "FK_OrganizationUnits_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OrganizationUnits_OrganizationUnits_TenantId_FacilityId_Par~" FOREIGN KEY ("TenantId", "FacilityId", "ParentId") REFERENCES "OrganizationUnits" ("TenantId", "FacilityId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_FacilityLocations_TenantId_FacilityId_Code" ON "FacilityLocations" ("TenantId", "FacilityId", "Code");

CREATE INDEX "IX_FacilityLocations_TenantId_FacilityId_ParentId" ON "FacilityLocations" ("TenantId", "FacilityId", "ParentId");

CREATE UNIQUE INDEX "IX_FacilityOperatingSettings_TenantId_FacilityId_Revision" ON "FacilityOperatingSettings" ("TenantId", "FacilityId", "Revision");

CREATE UNIQUE INDEX "IX_OrganizationUnits_TenantId_FacilityId_Code" ON "OrganizationUnits" ("TenantId", "FacilityId", "Code");

CREATE INDEX "IX_OrganizationUnits_TenantId_FacilityId_ParentId" ON "OrganizationUnits" ("TenantId", "FacilityId", "ParentId");

DO $guard$ DECLARE t text; BEGIN
  FOREACH t IN ARRAY ARRAY['OrganizationUnits','FacilityLocations','FacilityOperatingSettings'] LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',t);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',t);
    EXECUTE format('CREATE POLICY organization_tenant ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',t);
  END LOOP;
END $guard$;
CREATE FUNCTION enforce_organization_node() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE ancestor_depth int; descendant_depth int; invalid_path boolean; parent_active boolean; has_child boolean;
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Organization history cannot be deleted' USING ERRCODE='23514'; END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended('organization-db:'||NEW."TenantId"::text||':'||NEW."FacilityId"::text,0));
  PERFORM "Id" FROM "Facilities" WHERE "Id"=NEW."FacilityId" AND "TenantId"=NEW."TenantId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
  IF NEW."IsDeleted" OR NEW."Revision"<1 OR NEW."Code" !~ '^[A-Z0-9_-]{1,64}$' OR length(btrim(NEW."Name"))=0
    OR (TG_TABLE_NAME='OrganizationUnits' AND NEW."Kind" NOT IN ('department','unit'))
    OR (TG_TABLE_NAME='FacilityLocations' AND NEW."Kind"<>'location') THEN
    RAISE EXCEPTION 'Invalid retained organization node' USING ERRCODE='23514'; END IF;
  IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."TenantId"<>OLD."TenantId" OR NEW."FacilityId"<>OLD."FacilityId"
    OR NEW."Code"<>OLD."Code" OR NEW."CreatedAt"<>OLD."CreatedAt" OR NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy"
    OR NEW."Revision"<>OLD."Revision"+1) THEN
    RAISE EXCEPTION 'Organization identity/provenance is immutable; revision must advance once' USING ERRCODE='23514'; END IF;
  IF TG_OP='INSERT' AND NEW."Revision"<>1 THEN RAISE EXCEPTION 'Initial revision must be one' USING ERRCODE='23514'; END IF;
  ancestor_depth:=0;
  IF NEW."ParentId" IS NOT NULL THEN
    EXECUTE format('SELECT "IsActive" AND NOT "IsDeleted" FROM %I WHERE "TenantId"=$1 AND "FacilityId"=$2 AND "Id"=$3',TG_TABLE_NAME)
      INTO parent_active USING NEW."TenantId",NEW."FacilityId",NEW."ParentId";
    IF parent_active IS DISTINCT FROM true THEN RAISE EXCEPTION 'Parent must be active in the same facility' USING ERRCODE='23514'; END IF;
    EXECUTE format('WITH RECURSIVE a AS (
      SELECT "Id","ParentId",1 depth,ARRAY["Id"] path,false cycle FROM %I WHERE "TenantId"=$1 AND "FacilityId"=$2 AND "Id"=$3
      UNION ALL SELECT p."Id",p."ParentId",a.depth+1,a.path||p."Id",p."Id"=ANY(a.path)
      FROM %I p JOIN a ON p."Id"=a."ParentId" WHERE p."TenantId"=$1 AND p."FacilityId"=$2 AND a.depth<32 AND NOT a.cycle)
      SELECT COALESCE(max(depth),0),COALESCE(bool_or(cycle OR "Id"=$4 OR depth>=32),false) FROM a',TG_TABLE_NAME,TG_TABLE_NAME)
      INTO ancestor_depth,invalid_path USING NEW."TenantId",NEW."FacilityId",NEW."ParentId",NEW."Id";
    IF invalid_path THEN RAISE EXCEPTION 'Organization cycle or depth limit' USING ERRCODE='23514'; END IF;
  END IF;
  EXECUTE format('WITH RECURSIVE d AS (
    SELECT "Id",1 depth,ARRAY["Id"] path,false cycle FROM %I WHERE "TenantId"=$1 AND "FacilityId"=$2 AND "ParentId"=$3
    UNION ALL SELECT c."Id",d.depth+1,d.path||c."Id",c."Id"=ANY(d.path)
    FROM %I c JOIN d ON c."ParentId"=d."Id" WHERE c."TenantId"=$1 AND c."FacilityId"=$2 AND d.depth<32 AND NOT d.cycle)
    SELECT COALESCE(max(depth),0),COALESCE(bool_or(cycle OR "Id"=$3 OR depth>=32),false) FROM d',TG_TABLE_NAME,TG_TABLE_NAME)
    INTO descendant_depth,invalid_path USING NEW."TenantId",NEW."FacilityId",NEW."Id";
  IF invalid_path OR ancestor_depth+1+descendant_depth>32 THEN
    RAISE EXCEPTION 'Organization subtree exceeds maximum depth' USING ERRCODE='23514'; END IF;
  IF NOT NEW."IsActive" THEN
    EXECUTE format('SELECT EXISTS(SELECT 1 FROM %I WHERE "TenantId"=$1 AND "FacilityId"=$2 AND "ParentId"=$3 AND "IsActive" AND NOT "IsDeleted")',TG_TABLE_NAME)
      INTO has_child USING NEW."TenantId",NEW."FacilityId",NEW."Id";
    IF has_child THEN RAISE EXCEPTION 'Active children prevent deactivation' USING ERRCODE='23514'; END IF;
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER organization_node_guard BEFORE INSERT OR UPDATE OR DELETE ON "OrganizationUnits" FOR EACH ROW EXECUTE FUNCTION enforce_organization_node();
CREATE TRIGGER location_node_guard BEFORE INSERT OR UPDATE OR DELETE ON "FacilityLocations" FOR EACH ROW EXECUTE FUNCTION enforce_organization_node();
ALTER TABLE "FacilityOperatingSettings" ADD CONSTRAINT operating_revision_positive CHECK ("Revision">0 AND length(btrim("Reason"))>0);
CREATE FUNCTION validate_operating_settings_append() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE previous_revision bigint;
BEGIN
  PERFORM pg_advisory_xact_lock(734291);
  PERFORM "Id" FROM "Facilities" WHERE "Id"=NEW."FacilityId" AND "TenantId"=NEW."TenantId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(max("Revision"),0) INTO previous_revision FROM "FacilityOperatingSettings"
    WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId";
  IF NEW."Revision"<>previous_revision+1 OR NEW."IsDeleted" OR NOT NEW."IsActive" THEN
    RAISE EXCEPTION 'Operating settings require a consecutive retained revision' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER operating_settings_append BEFORE INSERT ON "FacilityOperatingSettings" FOR EACH ROW EXECUTE FUNCTION validate_operating_settings_append();
CREATE FUNCTION prevent_operating_settings_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Operating settings history is append-only' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER operating_settings_history BEFORE UPDATE OR DELETE ON "FacilityOperatingSettings" FOR EACH ROW EXECUTE FUNCTION prevent_operating_settings_rewrite();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('4c50f496-c9f0-447a-b53f-f7fd5a9b79b1','Organization.View','View organization structure','Facility scoped bounded structure',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
         ('9d4eb872-c88c-48b9-90e4-9e0d3b3258c2','Organization.Manage','Manage organization structure','Versioned retained facility structure',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN
  IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
    GRANT SELECT,INSERT,UPDATE ON "OrganizationUnits","FacilityLocations" TO cureflow_tenant;
    REVOKE DELETE ON "OrganizationUnits","FacilityLocations" FROM cureflow_tenant;
    GRANT SELECT,INSERT ON "FacilityOperatingSettings" TO cureflow_tenant;
    REVOKE UPDATE,DELETE ON "FacilityOperatingSettings" FROM cureflow_tenant;
  END IF;
END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007130052_AddOrganizationConfiguration', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "FacilityResources" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "LocationId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(64) NOT NULL,
    "ResourceType" character varying(32) NOT NULL,
    "Capacity" integer NOT NULL,
    "OperationalStatus" character varying(32) NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveUntil" timestamp with time zone,
    "Revision" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_FacilityResources" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_FacilityResources_FacilityLocations_TenantId_FacilityId_Loc~" FOREIGN KEY ("TenantId", "FacilityId", "LocationId") REFERENCES "FacilityLocations" ("TenantId", "FacilityId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_FacilityResources_TenantId_FacilityId_Code" ON "FacilityResources" ("TenantId", "FacilityId", "Code");

CREATE INDEX "IX_FacilityResources_TenantId_FacilityId_LocationId" ON "FacilityResources" ("TenantId", "FacilityId", "LocationId");

ALTER TABLE "FacilityResources" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "FacilityResources" FORCE ROW LEVEL SECURITY;
CREATE POLICY resource_tenant ON "FacilityResources" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "FacilityResources" ADD CONSTRAINT resource_configuration_valid CHECK (
  "Capacity" BETWEEN 1 AND 100000 AND "Revision">0 AND "Code" ~ '^[A-Z0-9_-]{1,64}$'
  AND "ResourceType" ~ '^[a-z0-9_-]{1,32}$' AND length(btrim("Name"))>0
  AND "OperationalStatus" IN ('enabled','disabled','maintenance')
  AND ("EffectiveUntil" IS NULL OR "EffectiveUntil">"EffectiveFrom"));
CREATE FUNCTION enforce_facility_resource() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Retained resources cannot be deleted' USING ERRCODE='23514'; END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended('organization-db:'||NEW."TenantId"::text||':'||NEW."FacilityId"::text,0));
  IF NEW."IsDeleted" OR (TG_OP='INSERT' AND NEW."Revision"<>1) THEN
    RAISE EXCEPTION 'Invalid retained resource revision' USING ERRCODE='23514'; END IF;
  IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."TenantId"<>OLD."TenantId" OR NEW."FacilityId"<>OLD."FacilityId" OR NEW."Code"<>OLD."Code"
    OR NEW."CreatedAt"<>OLD."CreatedAt" OR NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy" OR NEW."Revision"<>OLD."Revision"+1) THEN
    RAISE EXCEPTION 'Resource identity/provenance must be retained' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "FacilityLocations" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Id"=NEW."LocationId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active same-facility location required' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER resource_guard BEFORE INSERT OR UPDATE OR DELETE ON "FacilityResources" FOR EACH ROW EXECUTE FUNCTION enforce_facility_resource();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT,UPDATE ON "FacilityResources" TO cureflow_tenant;
  REVOKE DELETE ON "FacilityResources" FROM cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007131452_AddFacilityResources', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "OrganizationStructureVersions" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "NodeId" uuid NOT NULL,
    "NodeCategory" character varying(16) NOT NULL,
    "Revision" bigint NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "ParentId" uuid,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(64) NOT NULL,
    "Kind" character varying(32) NOT NULL,
    "NodeIsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_OrganizationStructureVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OrganizationStructureVersions_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_OrganizationStructureVersions_TenantId_FacilityId_NodeCate~1" ON "OrganizationStructureVersions" ("TenantId", "FacilityId", "NodeCategory", "NodeId", "Revision");

CREATE INDEX "IX_OrganizationStructureVersions_TenantId_FacilityId_NodeCateg~" ON "OrganizationStructureVersions" ("TenantId", "FacilityId", "NodeCategory", "NodeId", "EffectiveFrom");

ALTER TABLE "OrganizationStructureVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "OrganizationStructureVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY structure_history_tenant ON "OrganizationStructureVersions"
  USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "OrganizationStructureVersions" ADD CONSTRAINT structure_version_valid CHECK ("Revision">0 AND "NodeCategory" IN ('units','locations'));
CREATE FUNCTION retain_organization_structure_version() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public,pg_temp AS $body$
BEGIN
  INSERT INTO public."OrganizationStructureVersions" ("Id","TenantId","FacilityId","NodeId","NodeCategory","Revision","EffectiveFrom","ParentId","Name","Code","Kind","NodeIsActive","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted")
    VALUES (gen_random_uuid(),NEW."TenantId",NEW."FacilityId",NEW."Id",CASE WHEN TG_TABLE_NAME='OrganizationUnits' THEN 'units' ELSE 'locations' END,
      NEW."Revision",clock_timestamp(),NEW."ParentId",NEW."Name",NEW."Code",NEW."Kind",NEW."IsActive",clock_timestamp(),clock_timestamp(),COALESCE(NEW."UpdatedBy",NEW."CreatedBy"),NULL,true,false);
  RETURN NEW;
END $body$;
REVOKE ALL ON FUNCTION retain_organization_structure_version() FROM PUBLIC;
CREATE TRIGGER retain_unit_structure AFTER INSERT OR UPDATE ON "OrganizationUnits" FOR EACH ROW EXECUTE FUNCTION retain_organization_structure_version();
CREATE TRIGGER retain_location_structure AFTER INSERT OR UPDATE ON "FacilityLocations" FOR EACH ROW EXECUTE FUNCTION retain_organization_structure_version();
CREATE FUNCTION prevent_structure_history_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Organization structure history is immutable' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER structure_history_immutable BEFORE UPDATE OR DELETE ON "OrganizationStructureVersions" FOR EACH ROW EXECUTE FUNCTION prevent_structure_history_rewrite();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT ON "OrganizationStructureVersions" TO cureflow_tenant;
  REVOKE INSERT,UPDATE,DELETE ON "OrganizationStructureVersions" FROM cureflow_tenant;
END IF; END $grant$;
-- No pre-rollout history backfill: effective identities must not be invented from current rows.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007132515_AddOrganizationStructureHistory', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Patients" ADD CONSTRAINT "AK_Patients_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "PatientIdentifierCounters" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "Namespace" character varying(64) NOT NULL,
    "CurrentValue" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientIdentifierCounters" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientIdentifierCounters_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "PatientIdentifiers" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Namespace" character varying(64) NOT NULL,
    "Value" character varying(64) NOT NULL,
    "Sequence" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientIdentifiers" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientIdentifiers_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientIdentifiers_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientIdentifierCounters_TenantId_FacilityId_Namespace" ON "PatientIdentifierCounters" ("TenantId", "FacilityId", "Namespace");

CREATE UNIQUE INDEX "IX_PatientIdentifiers_TenantId_FacilityId_Namespace_PatientId" ON "PatientIdentifiers" ("TenantId", "FacilityId", "Namespace", "PatientId");

CREATE UNIQUE INDEX "IX_PatientIdentifiers_TenantId_FacilityId_Namespace_Value" ON "PatientIdentifiers" ("TenantId", "FacilityId", "Namespace", "Value");

CREATE INDEX "IX_PatientIdentifiers_TenantId_PatientId" ON "PatientIdentifiers" ("TenantId", "PatientId");

DO $rls$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['PatientIdentifiers','PatientIdentifierCounters'] LOOP
  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',t);
  EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',t);
  EXECUTE format('CREATE POLICY identifier_tenant ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',t);
END LOOP; END $rls$;
ALTER TABLE "PatientIdentifierCounters" ADD CONSTRAINT local_counter_valid CHECK ("Namespace"='local-mrn' AND "CurrentValue">0 AND NOT "IsDeleted");
ALTER TABLE "PatientIdentifiers" ADD CONSTRAINT local_identifier_valid CHECK ("Namespace"='local-mrn' AND "Sequence">0 AND NOT "IsDeleted"
  AND "Value"='MRN-'||CASE WHEN length("Sequence"::text)<10 THEN lpad("Sequence"::text,10,'0') ELSE "Sequence"::text END);
CREATE FUNCTION enforce_identifier_counter() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Identifier counters cannot be deleted' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required for namespace counter' USING ERRCODE='23514'; END IF;
  IF NOT NEW."IsActive" THEN RAISE EXCEPTION 'Namespace counter cannot be disabled/reset' USING ERRCODE='23514'; END IF;
  IF TG_OP='INSERT' AND NEW."CurrentValue"<>1 THEN RAISE EXCEPTION 'Counter must begin at one' USING ERRCODE='23514'; END IF;
  IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."TenantId"<>OLD."TenantId" OR NEW."FacilityId"<>OLD."FacilityId" OR NEW."Namespace"<>OLD."Namespace"
    OR NEW."CreatedAt"<>OLD."CreatedAt" OR NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy" OR NEW."CurrentValue"<>OLD."CurrentValue"+1) THEN
    RAISE EXCEPTION 'Identifier counter cannot reset or change namespace' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER identifier_counter_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientIdentifierCounters" FOR EACH ROW EXECUTE FUNCTION enforce_identifier_counter();
CREATE FUNCTION prevent_identifier_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Issued identifiers are immutable and cannot be reused' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER identifier_immutable BEFORE UPDATE OR DELETE ON "PatientIdentifiers" FOR EACH ROW EXECUTE FUNCTION prevent_identifier_rewrite();
CREATE FUNCTION validate_identifier_allocation() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE current_value bigint;
BEGIN
  SELECT "CurrentValue" INTO current_value FROM "PatientIdentifierCounters" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Namespace"=NEW."Namespace" FOR SHARE;
  IF current_value IS DISTINCT FROM NEW."Sequence" THEN RAISE EXCEPTION 'Identifier requires current allocated counter value' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active patient required' USING ERRCODE='23514'; END IF;
  PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active owning facility required' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER identifier_allocation_guard BEFORE INSERT ON "PatientIdentifiers" FOR EACH ROW EXECUTE FUNCTION validate_identifier_allocation();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "PatientIdentifiers" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PatientIdentifiers" FROM cureflow_tenant;
  GRANT SELECT,INSERT,UPDATE ON "PatientIdentifierCounters" TO cureflow_tenant;
  REVOKE DELETE ON "PatientIdentifierCounters" FROM cureflow_tenant;
END IF; END $grant$;
-- No retroactive MRNs, source identifier copying, resets or clinical-row identity replacement.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007133330_AddLocalPatientIdentifiers', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "PatientContactVersions" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "ContactId" uuid NOT NULL,
    "Revision" bigint NOT NULL,
    "ContactType" character varying(16) NOT NULL,
    "Usage" character varying(16) NOT NULL,
    "DisplayName" character varying(200) NOT NULL,
    "Value" character varying(254),
    "Relationship" character varying(100),
    "SourceReference" character varying(200),
    "VerificationStatus" character varying(16) NOT NULL,
    "Preferred" boolean NOT NULL,
    "ContactIsActive" boolean NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveUntil" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientContactVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientContactVersions_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientContactVersions_TenantId_PatientId_ContactId_Revision" ON "PatientContactVersions" ("TenantId", "PatientId", "ContactId", "Revision");

ALTER TABLE "PatientContactVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientContactVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY contact_tenant ON "PatientContactVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientContactVersions" ADD CONSTRAINT local_contact_valid CHECK (
  "Revision">0 AND "ContactType" IN ('phone','email','person') AND "Usage" IN ('normal','emergency','guardian')
  AND "VerificationStatus"='unverified' AND NOT "IsDeleted" AND "IsActive" AND length(btrim("DisplayName"))>0
  AND ("EffectiveUntil" IS NULL OR "EffectiveUntil">"EffectiveFrom")
  AND ("Usage"<>'guardian' OR COALESCE(length(btrim("Relationship")),0)>0)
  AND ("ContactType"<>'phone' OR COALESCE("Value" ~ '^\+?[0-9]{5,20}$' AND length("Value")<=20,false))
  AND ("ContactType"<>'email' OR COALESCE(length(btrim("Value")),0)>0)
  AND ("ContactType"<>'person' OR COALESCE(length(btrim("Value")),0)=0));
CREATE FUNCTION validate_local_contact_append() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE previous_revision bigint;
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('patient-contacts:'||NEW."TenantId"::text||':'||NEW."PatientId"::text,0));
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active scoped patient required for contact update' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(max("Revision"),0) INTO previous_revision FROM "PatientContactVersions"
    WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId" AND "ContactId"=NEW."ContactId";
  IF NEW."Revision"<>previous_revision+1 THEN RAISE EXCEPTION 'Contact history requires a consecutive revision' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER local_contact_append BEFORE INSERT ON "PatientContactVersions" FOR EACH ROW EXECUTE FUNCTION validate_local_contact_append();
CREATE FUNCTION prevent_local_contact_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Local contact history is append-only' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER local_contact_history BEFORE UPDATE OR DELETE ON "PatientContactVersions" FOR EACH ROW EXECUTE FUNCTION prevent_local_contact_rewrite();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "PatientContactVersions" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PatientContactVersions" FROM cureflow_tenant;
END IF; END $grant$;
-- No backfill, identity verification, portal proxy or global guardian authority.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007144416_AddLocalPatientContactVersions', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "PatientIdentityReviewVersions" (
    "Id" uuid NOT NULL,
    "ReviewId" uuid NOT NULL,
    "FirstPatientId" uuid NOT NULL,
    "SecondPatientId" uuid NOT NULL,
    "Revision" bigint NOT NULL,
    "Decision" character varying(32) NOT NULL,
    "EvidenceReference" character varying(200) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "FirstPatientTag" character varying(128) NOT NULL,
    "SecondPatientTag" character varying(128) NOT NULL,
    "ExactNameSignal" boolean NOT NULL,
    "SharedPhoneSignal" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientIdentityReviewVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientIdentityReviewVersions_Patients_TenantId_FirstPatien~" FOREIGN KEY ("TenantId", "FirstPatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientIdentityReviewVersions_Patients_TenantId_SecondPatie~" FOREIGN KEY ("TenantId", "SecondPatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientIdentityReviewVersions_TenantId_FirstPatientId_Secon~" ON "PatientIdentityReviewVersions" ("TenantId", "FirstPatientId", "SecondPatientId", "Revision");

CREATE UNIQUE INDEX "IX_PatientIdentityReviewVersions_TenantId_ReviewId_Revision" ON "PatientIdentityReviewVersions" ("TenantId", "ReviewId", "Revision");

CREATE INDEX "IX_PatientIdentityReviewVersions_TenantId_SecondPatientId" ON "PatientIdentityReviewVersions" ("TenantId", "SecondPatientId");

ALTER TABLE "PatientIdentityReviewVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientIdentityReviewVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY identity_review_tenant ON "PatientIdentityReviewVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientIdentityReviewVersions" ADD CONSTRAINT identity_review_valid CHECK (
  "FirstPatientId"<"SecondPatientId" AND "Revision">0 AND "Decision" IN ('open','different','potential_same','inconclusive')
  AND length(btrim("EvidenceReference"))>0 AND length(btrim("Reason"))>0 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
CREATE FUNCTION validate_identity_review_append() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE previous_revision bigint; previous_review uuid; patient_count bigint;
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('identity-review:'||NEW."TenantId"::text||':'||NEW."FirstPatientId"::text||':'||NEW."SecondPatientId"::text,0));
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id" IN (NEW."FirstPatientId",NEW."SecondPatientId") AND "IsActive" AND NOT "IsDeleted" ORDER BY "Id" FOR SHARE;
  GET DIAGNOSTICS patient_count=ROW_COUNT;
  IF patient_count<>2 THEN RAISE EXCEPTION 'Two active scoped identities required' USING ERRCODE='23514'; END IF;
  SELECT "Revision","ReviewId" INTO previous_revision,previous_review FROM "PatientIdentityReviewVersions"
    WHERE "TenantId"=NEW."TenantId" AND "FirstPatientId"=NEW."FirstPatientId" AND "SecondPatientId"=NEW."SecondPatientId" ORDER BY "Revision" DESC LIMIT 1;
  IF previous_revision IS NULL THEN
    IF NEW."Revision"<>1 OR NEW."Decision"<>'open' THEN RAISE EXCEPTION 'New pair reviews begin open' USING ERRCODE='23514'; END IF;
  ELSIF NEW."Revision"<>previous_revision+1 OR NEW."ReviewId"<>previous_review THEN
    RAISE EXCEPTION 'Review identity and consecutive evidence must be retained' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER identity_review_append BEFORE INSERT ON "PatientIdentityReviewVersions" FOR EACH ROW EXECUTE FUNCTION validate_identity_review_append();
CREATE FUNCTION prevent_identity_review_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Identity review evidence is append-only' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER identity_review_history BEFORE UPDATE OR DELETE ON "PatientIdentityReviewVersions" FOR EACH ROW EXECUTE FUNCTION prevent_identity_review_rewrite();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('849fbf4f-cae0-470d-9011-2c4956a36d39','IdentityReview.View','Read local identity reviews','Minimum fields and protected review evidence',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
         ('d7114e87-3a89-43be-98e4-b20bfd182c87','IdentityReview.Manage','Manage local identity reviews','Explicit candidate review without merge authority',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "PatientIdentityReviewVersions" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PatientIdentityReviewVersions" FROM cureflow_tenant;
END IF; END $grant$;
-- Catalogue permissions only: no reviewer role assignments or identity merge authority are invented.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007145424_AddPatientIdentityReviews', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "PatientIdentityReviewVersions" ADD CONSTRAINT "AK_PatientIdentityReviewVersions_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "PatientMerges" (
    "Id" uuid NOT NULL,
    "SourcePatientId" uuid NOT NULL,
    "SurvivorPatientId" uuid NOT NULL,
    "ReviewVersionId" uuid NOT NULL,
    "SourcePatientTag" character varying(128) NOT NULL,
    "SurvivorPatientTag" character varying(128) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "MergedAt" timestamp with time zone NOT NULL,
    "Revision" bigint NOT NULL,
    "ReversedAt" timestamp with time zone,
    "ReversalReason" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientMerges" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_PatientMerges_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_PatientMerges_PatientIdentityReviewVersions_TenantId_Review~" FOREIGN KEY ("TenantId", "ReviewVersionId") REFERENCES "PatientIdentityReviewVersions" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientMerges_Patients_TenantId_SourcePatientId" FOREIGN KEY ("TenantId", "SourcePatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientMerges_Patients_TenantId_SurvivorPatientId" FOREIGN KEY ("TenantId", "SurvivorPatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "PatientLinkJournals" (
    "Id" uuid NOT NULL,
    "MergeId" uuid NOT NULL,
    "Operation" character varying(16) NOT NULL,
    "EntityType" character varying(80) NOT NULL,
    "EntityId" uuid NOT NULL,
    "BeforeValue" character varying(1000) NOT NULL,
    "AfterValue" character varying(1000) NOT NULL,
    "AppliedAt" timestamp with time zone NOT NULL,
    "PublicationHold" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientLinkJournals" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientLinkJournals_PatientMerges_TenantId_MergeId" FOREIGN KEY ("TenantId", "MergeId") REFERENCES "PatientMerges" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientLinkJournals_TenantId_MergeId_Operation" ON "PatientLinkJournals" ("TenantId", "MergeId", "Operation");

CREATE INDEX "IX_PatientMerges_TenantId_ReviewVersionId" ON "PatientMerges" ("TenantId", "ReviewVersionId");

CREATE UNIQUE INDEX "IX_PatientMerges_TenantId_SourcePatientId" ON "PatientMerges" ("TenantId", "SourcePatientId") WHERE "ReversedAt" IS NULL;

CREATE INDEX "IX_PatientMerges_TenantId_SurvivorPatientId" ON "PatientMerges" ("TenantId", "SurvivorPatientId");

ALTER TABLE "PatientMerges" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientMerges" FORCE ROW LEVEL SECURITY;
ALTER TABLE "PatientLinkJournals" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientLinkJournals" FORCE ROW LEVEL SECURITY;
CREATE POLICY patient_merge_tenant ON "PatientMerges" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_link_journal_tenant ON "PatientLinkJournals" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientMerges" ADD CONSTRAINT patient_merge_valid CHECK (
  "SourcePatientId"<>"SurvivorPatientId" AND "Revision"=1 AND "ReversedAt" IS NULL AND "ReversalReason" IS NULL
  AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "PatientLinkJournals" ADD CONSTRAINT patient_link_valid CHECK (
  "Operation"='merge' AND "EntityType"='PatientIdentityMapping' AND NOT "PublicationHold"
  AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
CREATE FUNCTION validate_patient_merge_insert() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE patient_count bigint; evidence record; ancestor_depth integer; first_id uuid; second_id uuid;
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('patient-mapping:'||NEW."TenantId"::text,0));
  first_id=LEAST(NEW."SourcePatientId",NEW."SurvivorPatientId");
  second_id=GREATEST(NEW."SourcePatientId",NEW."SurvivorPatientId");
  PERFORM pg_advisory_xact_lock(hashtextextended('identity-review:'||NEW."TenantId"::text||':'||first_id::text||':'||second_id::text,0));
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id" IN (first_id,second_id)
    AND "IsActive" AND NOT "IsDeleted" ORDER BY "Id" FOR SHARE;
  GET DIAGNOSTICS patient_count=ROW_COUNT;
  IF patient_count<>2 THEN RAISE EXCEPTION 'Two active scoped identities required' USING ERRCODE='23514'; END IF;
  SELECT * INTO evidence FROM "PatientIdentityReviewVersions"
    WHERE "TenantId"=NEW."TenantId" AND "FirstPatientId"=first_id AND "SecondPatientId"=second_id
    ORDER BY "Revision" DESC LIMIT 1 FOR SHARE;
  IF evidence."Id" IS DISTINCT FROM NEW."ReviewVersionId" OR evidence."Decision"<>'potential_same'
    OR evidence."CreatedBy" IS NULL OR evidence."CreatedBy"=NEW."CreatedBy"
    OR evidence."FirstPatientTag" IS DISTINCT FROM (CASE WHEN first_id=NEW."SourcePatientId" THEN NEW."SourcePatientTag" ELSE NEW."SurvivorPatientTag" END)
    OR evidence."SecondPatientTag" IS DISTINCT FROM (CASE WHEN second_id=NEW."SourcePatientId" THEN NEW."SourcePatientTag" ELSE NEW."SurvivorPatientTag" END) THEN
    RAISE EXCEPTION 'Current distinct review evidence required' USING ERRCODE='23514';
  END IF;
  IF EXISTS(SELECT 1 FROM "PatientMerges" WHERE "TenantId"=NEW."TenantId"
    AND "SourcePatientId" IN (first_id,second_id) AND "ReversedAt" IS NULL) THEN
    RAISE EXCEPTION 'Explicit canonical endpoints required' USING ERRCODE='23514';
  END IF;
  WITH RECURSIVE ancestors(id,depth) AS (
    SELECT NEW."SourcePatientId",0 UNION ALL SELECT m."SourcePatientId",a.depth+1 FROM "PatientMerges" m JOIN ancestors a ON m."SurvivorPatientId"=a.id
    WHERE m."TenantId"=NEW."TenantId" AND m."ReversedAt" IS NULL AND a.depth<32)
    SELECT COALESCE(MAX(depth),0) INTO ancestor_depth FROM ancestors;
  IF ancestor_depth>=31 THEN RAISE EXCEPTION 'Mapping depth requires review' USING ERRCODE='23514'; END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_merge_insert BEFORE INSERT ON "PatientMerges" FOR EACH ROW EXECUTE FUNCTION validate_patient_merge_insert();
CREATE FUNCTION prevent_patient_mapping_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Patient mapping evidence is retained' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER patient_merge_history BEFORE UPDATE OR DELETE ON "PatientMerges" FOR EACH ROW EXECUTE FUNCTION prevent_patient_mapping_rewrite();
CREATE TRIGGER patient_link_history BEFORE UPDATE OR DELETE ON "PatientLinkJournals" FOR EACH ROW EXECUTE FUNCTION prevent_patient_mapping_rewrite();
CREATE FUNCTION validate_patient_link_insert() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE mapping record;
BEGIN
  SELECT * INTO mapping FROM "PatientMerges" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."MergeId";
  IF mapping."Id" IS NULL OR NEW."EntityId"<>mapping."SourcePatientId" OR NEW."CreatedBy"<>mapping."CreatedBy"
     OR NEW."BeforeValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SourcePatientId"::text
     OR NEW."AfterValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SurvivorPatientId"::text THEN
    RAISE EXCEPTION 'Exact retained mapping journal required' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_link_insert BEFORE INSERT ON "PatientLinkJournals" FOR EACH ROW EXECUTE FUNCTION validate_patient_link_insert();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('d06f1860-b8fb-4138-b8be-b807fa51e21d','IdentityMerge.View','Resolve explicit local identity mappings','No automatic chart aggregation',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
         ('d4f147f7-6835-4f59-b94f-3fcf3816941d','IdentityMerge.Manage','Apply reviewed local identity mappings','Distinct review and retained source IDs',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "PatientMerges","PatientLinkJournals" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PatientMerges","PatientLinkJournals" FROM cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007153620_AddPatientCanonicalMappings', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "PatientMerges" DROP CONSTRAINT patient_merge_valid;
ALTER TABLE "PatientMerges" ADD CONSTRAINT patient_merge_valid CHECK (
  "SourcePatientId"<>"SurvivorPatientId" AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0
  AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted"
  AND (("Revision"=1 AND "ReversedAt" IS NULL AND "ReversalReason" IS NULL)
    OR ("Revision"=2 AND "ReversedAt">="MergedAt" AND length(btrim("ReversalReason"))>0 AND "UpdatedBy" IS NOT NULL)));
ALTER TABLE "PatientLinkJournals" DROP CONSTRAINT patient_link_valid;
ALTER TABLE "PatientLinkJournals" ADD CONSTRAINT patient_link_valid CHECK (
  "Operation" IN ('merge','reverse') AND "EntityType"='PatientIdentityMapping'
  AND "PublicationHold"=("Operation"='reverse') AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
DROP TRIGGER patient_merge_history ON "PatientMerges";
CREATE FUNCTION validate_patient_mapping_change() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Mappings cannot be deleted' USING ERRCODE='23514'; END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended('patient-mapping:'||NEW."TenantId"::text,0));
  IF TG_OP='INSERT' THEN
    IF NEW."Revision"<>1 OR NEW."ReversedAt" IS NOT NULL THEN RAISE EXCEPTION 'New mapping must be active' USING ERRCODE='23514'; END IF;
    IF EXISTS(SELECT 1 FROM "PatientLinkJournals" j JOIN "PatientMerges" m ON m."TenantId"=j."TenantId" AND m."Id"=j."MergeId"
      WHERE m."TenantId"=NEW."TenantId" AND (m."SourcePatientId" IN (NEW."SourcePatientId",NEW."SurvivorPatientId")
        OR m."SurvivorPatientId" IN (NEW."SourcePatientId",NEW."SurvivorPatientId")) AND j."PublicationHold") THEN
      RAISE EXCEPTION 'Correction publication hold requires review' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
  END IF;
  IF OLD."ReversedAt" IS NOT NULL OR NEW."ReversedAt" IS NULL OR NEW."Revision"<>OLD."Revision"+1
    OR (to_jsonb(NEW)-ARRAY['ReversedAt','ReversalReason','Revision','UpdatedAt','UpdatedBy'])
       IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['ReversedAt','ReversalReason','Revision','UpdatedAt','UpdatedBy']) THEN
    RAISE EXCEPTION 'Only a retained single reversal is permitted' USING ERRCODE='23514';
  END IF;
  IF EXISTS(SELECT 1 FROM "PatientMerges" m WHERE m."TenantId"=NEW."TenantId" AND m."Id"<>NEW."Id" AND m."ReversedAt" IS NULL
    AND (m."SourcePatientId" IN (NEW."SourcePatientId",NEW."SurvivorPatientId") OR m."SurvivorPatientId" IN (NEW."SourcePatientId",NEW."SurvivorPatientId"))) THEN
    RAISE EXCEPTION 'Dependent mappings require reconciliation review' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_merge_history BEFORE INSERT OR UPDATE OR DELETE ON "PatientMerges" FOR EACH ROW EXECUTE FUNCTION validate_patient_mapping_change();
CREATE OR REPLACE FUNCTION validate_patient_link_insert() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE mapping record; evidence record; first_id uuid; second_id uuid;
BEGIN
  SELECT * INTO mapping FROM "PatientMerges" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."MergeId";
  IF mapping."Id" IS NULL OR NEW."EntityId"<>mapping."SourcePatientId" THEN
    RAISE EXCEPTION 'Exact retained mapping journal required' USING ERRCODE='23514';
  END IF;
  IF NEW."Operation"='merge' THEN
    IF NEW."CreatedBy"<>mapping."CreatedBy"
       OR NEW."BeforeValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SourcePatientId"::text
       OR NEW."AfterValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SurvivorPatientId"::text THEN
      RAISE EXCEPTION 'Exact merge journal required' USING ERRCODE='23514';
    END IF;
  ELSE
    first_id=LEAST(mapping."SourcePatientId",mapping."SurvivorPatientId");
    second_id=GREATEST(mapping."SourcePatientId",mapping."SurvivorPatientId");
    PERFORM pg_advisory_xact_lock(hashtextextended('identity-review:'||NEW."TenantId"::text||':'||first_id::text||':'||second_id::text,0));
    SELECT * INTO evidence FROM "PatientIdentityReviewVersions" WHERE "TenantId"=NEW."TenantId"
      AND "FirstPatientId"=first_id AND "SecondPatientId"=second_id ORDER BY "Revision" DESC LIMIT 1 FOR SHARE;
    IF mapping."ReversedAt" IS NULL OR NEW."CreatedBy" IS DISTINCT FROM mapping."UpdatedBy"
       OR NEW."BeforeValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SurvivorPatientId"::text
       OR NEW."AfterValue"::jsonb->>'canonicalPatientId' IS DISTINCT FROM mapping."SourcePatientId"::text
       OR evidence."Id" IS NULL OR evidence."Decision"<>'different' OR evidence."CreatedAt"<=mapping."MergedAt"
       OR evidence."CreatedBy" IS NULL OR evidence."CreatedBy"=NEW."CreatedBy"
       OR NEW."AfterValue"::jsonb->>'reviewVersionId' IS DISTINCT FROM evidence."Id"::text
       OR NEW."AfterValue"::jsonb->>'reviewRevision' IS DISTINCT FROM evidence."Revision"::text
       OR NOT NEW."PublicationHold" THEN
      RAISE EXCEPTION 'Subsequent distinct correction review and publication hold required' USING ERRCODE='23514';
    END IF;
  END IF;
  RETURN NEW;
END $body$;
CREATE FUNCTION require_patient_mapping_journal() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF NOT EXISTS(SELECT 1 FROM "PatientLinkJournals" WHERE "TenantId"=NEW."TenantId" AND "MergeId"=NEW."Id" AND "Operation"='merge')
    OR (NEW."ReversedAt" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "PatientLinkJournals"
      WHERE "TenantId"=NEW."TenantId" AND "MergeId"=NEW."Id" AND "Operation"='reverse' AND "PublicationHold")) THEN
    RAISE EXCEPTION 'Mapping change requires retained journal in the same transaction' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE CONSTRAINT TRIGGER patient_mapping_journal_required AFTER INSERT OR UPDATE ON "PatientMerges"
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_patient_mapping_journal();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('f91dd62c-86de-480c-952d-fd21e2645eb0','IdentityMerge.Reverse','Reverse reviewed local identity mappings','Preserved correction journal and publication hold',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  REVOKE UPDATE ON "PatientMerges" FROM cureflow_tenant;
  GRANT UPDATE ("ReversedAt","ReversalReason","Revision","UpdatedAt","UpdatedBy") ON "PatientMerges" TO cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007154046_AddPatientMappingReversals', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Patients" ADD "BirthDateDay" integer;

ALTER TABLE "Patients" ADD "BirthDateMonth" integer;

ALTER TABLE "Patients" ADD "BirthDatePrecision" character varying(24) NOT NULL DEFAULT 'legacy_unspecified';

ALTER TABLE "Patients" ADD "BirthDateYear" integer;

CREATE TABLE "PatientDemographicVersions" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Revision" bigint NOT NULL,
    "SourceType" character varying(32) NOT NULL,
    "SourceReference" character varying(200),
    "Reason" character varying(500) NOT NULL,
    "VerificationStatus" character varying(24) NOT NULL,
    "BeforeValue" text,
    "AfterValue" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientDemographicVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientDemographicVersions_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientDemographicVersions_TenantId_PatientId_Revision" ON "PatientDemographicVersions" ("TenantId", "PatientId", "Revision");

ALTER TABLE "PatientDemographicVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientDemographicVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY patient_demographic_tenant ON "PatientDemographicVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientDemographicVersions" ADD CONSTRAINT patient_demographic_valid CHECK (
  "Revision">0 AND "VerificationStatus"='unverified' AND "SourceType" IN ('local_application','patient_reported','guardian_reported','document_reported','staff_observed','import')
  AND length(btrim("Reason"))>0 AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "Patients" ADD CONSTRAINT patient_birth_precision_valid CHECK (COALESCE((
  ("BirthDatePrecision" IN ('legacy_unspecified','unknown') AND "BirthDateYear" IS NULL AND "BirthDateMonth" IS NULL AND "BirthDateDay" IS NULL)
  OR ("BirthDatePrecision" IN ('year','estimated_year') AND "BirthDateYear" BETWEEN 1 AND 9999 AND "BirthDateMonth" IS NULL AND "BirthDateDay" IS NULL)
  OR ("BirthDatePrecision" IN ('month','estimated_month') AND "BirthDateYear" BETWEEN 1 AND 9999 AND "BirthDateMonth" BETWEEN 1 AND 12 AND "BirthDateDay" IS NULL)
  OR ("BirthDatePrecision" IN ('day','estimated_day') AND "BirthDateYear" BETWEEN 1 AND 9999 AND "BirthDateMonth" BETWEEN 1 AND 12 AND "BirthDateDay" BETWEEN 1 AND 31)),false));
ALTER TABLE "Patients" ADD CONSTRAINT patient_calendar_birth_valid CHECK (
  CASE WHEN "BirthDatePrecision" IN ('day','estimated_day') AND "BirthDateYear" BETWEEN 1 AND 9999 AND "BirthDateMonth" BETWEEN 1 AND 12 THEN
    "BirthDateDay"<=EXTRACT(DAY FROM (make_date("BirthDateYear","BirthDateMonth",1)+INTERVAL '1 month'-INTERVAL '1 day'))
    ELSE "BirthDatePrecision" NOT IN ('day','estimated_day') END);
ALTER TABLE "Patients" ADD CONSTRAINT patient_birth_representations_valid CHECK ("BirthDatePrecision"='legacy_unspecified' OR "DateOfBirth" IS NULL);
CREATE FUNCTION capture_patient_demographic_change() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
  SET search_path=pg_catalog,public,pg_temp AS $body$
DECLARE before_value jsonb; after_value jsonb; excluded text[]; next_revision bigint; source_type text; source_ref text; reason text;
BEGIN
  excluded=ARRAY['IdentityStatus','TenantId','Id','CreatedAt','UpdatedAt','CreatedBy','UpdatedBy','IsActive','IsDeleted','SourceLeadId','EmailNotificationsEnabled',
    'Department','InquirySource','ReferralDoctor','ReferringDoctorId','AssignedStaffId','Status','Tags','Notes','LastContactAt','FollowUpDate','AiSummary','AiLeadScore'];
  after_value=to_jsonb(NEW)-excluded;
  IF TG_OP='UPDATE' THEN before_value=to_jsonb(OLD)-excluded; END IF;
  source_type=COALESCE(NULLIF(current_setting('app.demographic_source_type',true),''),'local_application');
  source_ref=NULLIF(current_setting('app.demographic_source_reference',true),'');
  reason=COALESCE(NULLIF(current_setting('app.demographic_reason',true),''),'Local application demographic write');
  -- Consume local metadata even when demographics did not change, avoiding source reuse by a later command.
  PERFORM set_config('app.demographic_source_type','',true);
  PERFORM set_config('app.demographic_source_reference','',true);
  PERFORM set_config('app.demographic_reason','',true);
  IF TG_OP='UPDATE' AND before_value=after_value THEN RETURN NEW; END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended('patient-demographics:'||NEW."TenantId"::text||':'||NEW."Id"::text,0));
  SELECT COALESCE(MAX("Revision"),0)+1 INTO next_revision FROM public."PatientDemographicVersions"
    WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."Id";
  INSERT INTO public."PatientDemographicVersions" ("Id","TenantId","PatientId","Revision","SourceType","SourceReference","Reason","VerificationStatus",
    "BeforeValue","AfterValue","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted")
    VALUES (gen_random_uuid(),NEW."TenantId",NEW."Id",next_revision,source_type,source_ref,reason,'unverified',before_value::text,after_value::text,
      CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,CASE WHEN TG_OP='INSERT' THEN NEW."CreatedBy" ELSE NEW."UpdatedBy" END,NULL,true,false);
  RETURN NEW;
END $body$;
REVOKE ALL ON FUNCTION capture_patient_demographic_change() FROM PUBLIC;
CREATE TRIGGER patient_demographic_capture AFTER INSERT OR UPDATE ON "Patients" FOR EACH ROW EXECUTE FUNCTION capture_patient_demographic_change();
CREATE FUNCTION prevent_patient_demographic_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Demographic evidence is append-only' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER patient_demographic_history BEFORE UPDATE OR DELETE ON "PatientDemographicVersions"
  FOR EACH ROW EXECUTE FUNCTION prevent_patient_demographic_rewrite();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT ON "PatientDemographicVersions" TO cureflow_tenant;
  REVOKE INSERT,UPDATE,DELETE ON "PatientDemographicVersions" FROM cureflow_tenant;
END IF; END $grant$;
-- Existing DOB timestamps, patient IDs and source associations are unchanged; no historical evidence is fabricated.

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007154947_AddPatientDemographicProvenance', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Patients" ADD "IdentityStatus" character varying(24) NOT NULL DEFAULT 'unreviewed';

ALTER TABLE "PatientDemographicVersions" ADD CONSTRAINT "AK_PatientDemographicVersions_TenantId_PatientId_Id" UNIQUE ("TenantId", "PatientId", "Id");

CREATE TABLE "PatientIdentityVerifications" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "DemographicVersionId" uuid NOT NULL,
    "ProofReference" character varying(200) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "PatientTag" character varying(128) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientIdentityVerifications" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientIdentityVerifications_PatientDemographicVersions_Ten~" FOREIGN KEY ("TenantId", "PatientId", "DemographicVersionId") REFERENCES "PatientDemographicVersions" ("TenantId", "PatientId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "TemporaryPatientIdentities" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "Value" character varying(40) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "SourceReference" character varying(200),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_TemporaryPatientIdentities" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TemporaryPatientIdentities_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_TemporaryPatientIdentities_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_PatientIdentityVerifications_TenantId_PatientId_CreatedAt" ON "PatientIdentityVerifications" ("TenantId", "PatientId", "CreatedAt");

CREATE INDEX "IX_PatientIdentityVerifications_TenantId_PatientId_Demographic~" ON "PatientIdentityVerifications" ("TenantId", "PatientId", "DemographicVersionId");

CREATE UNIQUE INDEX "IX_TemporaryPatientIdentities_TenantId_FacilityId_Value" ON "TemporaryPatientIdentities" ("TenantId", "FacilityId", "Value");

CREATE UNIQUE INDEX "IX_TemporaryPatientIdentities_TenantId_PatientId" ON "TemporaryPatientIdentities" ("TenantId", "PatientId");

ALTER TABLE "TemporaryPatientIdentities" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "TemporaryPatientIdentities" FORCE ROW LEVEL SECURITY;
ALTER TABLE "PatientIdentityVerifications" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientIdentityVerifications" FORCE ROW LEVEL SECURITY;
CREATE POLICY temporary_patient_tenant ON "TemporaryPatientIdentities" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_verification_tenant ON "PatientIdentityVerifications" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "Patients" ADD CONSTRAINT patient_identity_status_valid CHECK ("IdentityStatus" IN ('unknown','unreviewed','verified_local'));
ALTER TABLE "TemporaryPatientIdentities" ADD CONSTRAINT temporary_patient_valid CHECK (
  "Value"='TEMP-'||replace("PatientId"::text,'-','') AND length(btrim("Reason"))>0 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "PatientIdentityVerifications" ADD CONSTRAINT patient_verification_valid CHECK (
  length(btrim("ProofReference"))>0 AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0
  AND length("PatientTag") BETWEEN 80 AND 128 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
CREATE FUNCTION validate_temporary_patient_intake() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF NOT EXISTS(SELECT 1 FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted")
    OR NOT EXISTS(SELECT 1 FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId"
      AND "IdentityStatus"='unknown' AND "BirthDatePrecision"='unknown' AND "Phone"='' AND "DateOfBirth" IS NULL AND "Age" IS NULL
      AND "IsActive" AND NOT "IsDeleted") THEN
    RAISE EXCEPTION 'Unknown intake requires active local facility/patient without guessed demographics' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER temporary_patient_insert BEFORE INSERT ON "TemporaryPatientIdentities" FOR EACH ROW EXECUTE FUNCTION validate_temporary_patient_intake();
CREATE FUNCTION validate_patient_local_verification() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE current_version uuid;
BEGIN
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId"
    AND "IdentityStatus"='verified_local' AND "UpdatedBy"=NEW."CreatedBy" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Current local patient/reviewer attribution required' USING ERRCODE='23514'; END IF;
  SELECT "Id" INTO current_version FROM "PatientDemographicVersions" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId"
    ORDER BY "Revision" DESC LIMIT 1;
  IF current_version IS DISTINCT FROM NEW."DemographicVersionId" THEN
    RAISE EXCEPTION 'Current demographic source evidence required' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_verification_insert BEFORE INSERT ON "PatientIdentityVerifications" FOR EACH ROW EXECUTE FUNCTION validate_patient_local_verification();
CREATE FUNCTION prevent_patient_identity_evidence_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Temporary identity and verification evidence are retained' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER temporary_patient_history BEFORE UPDATE OR DELETE ON "TemporaryPatientIdentities"
  FOR EACH ROW EXECUTE FUNCTION prevent_patient_identity_evidence_rewrite();
CREATE TRIGGER patient_verification_history BEFORE UPDATE OR DELETE ON "PatientIdentityVerifications"
  FOR EACH ROW EXECUTE FUNCTION prevent_patient_identity_evidence_rewrite();
CREATE FUNCTION validate_patient_identity_transition() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF TG_OP='INSERT' THEN
    IF NEW."IdentityStatus"='verified_local' THEN RAISE EXCEPTION 'New registrations begin unreviewed' USING ERRCODE='23514'; END IF;
  ELSIF NEW."IdentityStatus" IS DISTINCT FROM OLD."IdentityStatus" THEN
    IF NEW."IdentityStatus"<>'verified_local' OR OLD."IdentityStatus" NOT IN ('unknown','unreviewed') OR NEW."UpdatedBy" IS NULL THEN
      RAISE EXCEPTION 'Identity workflow transition requires reviewed verification' USING ERRCODE='23514';
    END IF;
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_identity_transition BEFORE INSERT OR UPDATE ON "Patients" FOR EACH ROW EXECUTE FUNCTION validate_patient_identity_transition();
CREATE FUNCTION require_patient_identity_verification() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF NOT EXISTS(SELECT 1 FROM "PatientIdentityVerifications" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."Id"
    AND "CreatedBy"=NEW."UpdatedBy" AND "CreatedAt">=NEW."UpdatedAt") THEN
    RAISE EXCEPTION 'Local verification requires evidence in the same transaction' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE CONSTRAINT TRIGGER patient_identity_verification_required AFTER UPDATE ON "Patients"
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
  WHEN (NEW."IdentityStatus"='verified_local' AND OLD."IdentityStatus" IS DISTINCT FROM NEW."IdentityStatus")
  EXECUTE FUNCTION require_patient_identity_verification();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('0f79b2ee-67d6-4f55-9b70-f02b3ac4b723','PatientIdentity.Verify','Review local patient identity evidence','Configured accountable local review; no portal/sharing authority',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "TemporaryPatientIdentities","PatientIdentityVerifications" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "TemporaryPatientIdentities","PatientIdentityVerifications" FROM cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007155908_AddUnknownPatientIdentityVerification', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "PatientAlertVersions" (
    "Id" uuid NOT NULL,
    "AlertId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Key" character varying(80) NOT NULL,
    "Category" character varying(32) NOT NULL,
    "Revision" bigint NOT NULL,
    "Title" character varying(120) NOT NULL,
    "Detail" character varying(1000),
    "Severity" character varying(16) NOT NULL,
    "AlertIsActive" boolean NOT NULL,
    "SourceType" character varying(32) NOT NULL,
    "SourceReference" character varying(200),
    "Reason" character varying(500) NOT NULL,
    "ReportedDeathDate" date,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientAlertVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientAlertVersions_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "PatientConfidentialityStates" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Restricted" boolean NOT NULL,
    "Revision" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientConfidentialityStates" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientConfidentialityStates_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientAlertVersions_TenantId_AlertId_Revision" ON "PatientAlertVersions" ("TenantId", "AlertId", "Revision");

CREATE UNIQUE INDEX "IX_PatientAlertVersions_TenantId_PatientId_Category_Key_Revisi~" ON "PatientAlertVersions" ("TenantId", "PatientId", "Category", "Key", "Revision");

CREATE UNIQUE INDEX "IX_PatientConfidentialityStates_TenantId_PatientId" ON "PatientConfidentialityStates" ("TenantId", "PatientId");

ALTER TABLE "PatientConfidentialityStates" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientConfidentialityStates" FORCE ROW LEVEL SECURITY;
ALTER TABLE "PatientAlertVersions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientAlertVersions" FORCE ROW LEVEL SECURITY;
CREATE POLICY patient_confidentiality_tenant ON "PatientConfidentialityStates" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_alert_tenant ON "PatientAlertVersions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
  WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "PatientConfidentialityStates" ADD CONSTRAINT patient_confidentiality_revision_valid CHECK ("Revision">0 AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "PatientAlertVersions" ADD CONSTRAINT patient_alert_valid CHECK (
  "Revision">0 AND "Category" IN ('identity','clinical','confidentiality','deceased_reported') AND "Severity" IN ('info','warning','critical')
  AND "SourceType" IN ('local_application','patient_reported','guardian_reported','document_reported','staff_observed','import')
  AND length(btrim("Title"))>0 AND length(btrim("Key"))>0 AND length(btrim("Reason"))>0 AND length(btrim("PolicyVersion"))>0
  AND ("Category"<>'confidentiality' OR "Key"='chart') AND ("Category"<>'deceased_reported' OR "Key"='status')
  AND ("ReportedDeathDate" IS NULL OR "Category"='deceased_reported') AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
CREATE FUNCTION validate_patient_alert_append() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE prior record; head_count bigint;
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('patient-alerts:'||NEW."TenantId"::text||':'||NEW."PatientId"::text,0));
  PERFORM "Id" FROM "Patients" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."PatientId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active scoped patient required' USING ERRCODE='23514'; END IF;
  SELECT "AlertId","Revision" INTO prior FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId"
    AND "PatientId"=NEW."PatientId" AND "Category"=NEW."Category" AND "Key"=NEW."Key" ORDER BY "Revision" DESC LIMIT 1;
  IF prior."Revision" IS NULL THEN
    IF NEW."Revision"<>1 THEN RAISE EXCEPTION 'New alert starts at revision one' USING ERRCODE='23514'; END IF;
    SELECT COUNT(*) INTO head_count FROM (SELECT DISTINCT "Category","Key" FROM "PatientAlertVersions"
      WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId") heads;
    IF head_count>=100 THEN RAISE EXCEPTION 'Patient alert count requires reconciliation' USING ERRCODE='23514'; END IF;
  ELSIF NEW."AlertId"<>prior."AlertId" OR NEW."Revision"<>prior."Revision"+1 THEN
    RAISE EXCEPTION 'Stable alert identity/consecutive evidence required' USING ERRCODE='23514';
  END IF;
  IF EXISTS(SELECT 1 FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId" AND "AlertId"=NEW."AlertId"
    AND ("PatientId"<>NEW."PatientId" OR "Category"<>NEW."Category" OR "Key"<>NEW."Key")) THEN
    RAISE EXCEPTION 'Alert ownership/type/key cannot change' USING ERRCODE='23514';
  END IF;
  NEW."EffectiveFrom"=clock_timestamp();
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_alert_append BEFORE INSERT ON "PatientAlertVersions" FOR EACH ROW EXECUTE FUNCTION validate_patient_alert_append();
CREATE FUNCTION prevent_patient_alert_rewrite() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN RAISE EXCEPTION 'Patient alert evidence is append-only' USING ERRCODE='23514'; END $body$;
CREATE TRIGGER patient_alert_history BEFORE UPDATE OR DELETE ON "PatientAlertVersions" FOR EACH ROW EXECUTE FUNCTION prevent_patient_alert_rewrite();
CREATE FUNCTION validate_patient_confidentiality_state() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE evidence record;
BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Chart confidentiality state is retained' USING ERRCODE='23514'; END IF;
  SELECT * INTO evidence FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId"
    AND "Category"='confidentiality' AND "Key"='chart' ORDER BY "Revision" DESC LIMIT 1;
  IF evidence."Id" IS NULL OR evidence."Revision"<>NEW."Revision" OR evidence."AlertIsActive"<>NEW."Restricted" THEN
    RAISE EXCEPTION 'Chart restriction must match latest reviewed alert evidence' USING ERRCODE='23514';
  END IF;
  IF TG_OP='INSERT' THEN
    IF NEW."Revision"<>1 THEN RAISE EXCEPTION 'Initial chart restriction requires first evidence' USING ERRCODE='23514'; END IF;
  ELSE
    IF NEW."Revision"<>OLD."Revision"+1 OR (to_jsonb(NEW)-ARRAY['Restricted','Revision','UpdatedAt','UpdatedBy'])
      IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Restricted','Revision','UpdatedAt','UpdatedBy']) THEN
      RAISE EXCEPTION 'Chart ownership and consecutive history must be retained' USING ERRCODE='23514';
    END IF;
  END IF;
  RETURN NEW;
END $body$;
CREATE TRIGGER patient_confidentiality_state_change BEFORE INSERT OR UPDATE OR DELETE ON "PatientConfidentialityStates"
  FOR EACH ROW EXECUTE FUNCTION validate_patient_confidentiality_state();
CREATE FUNCTION require_patient_confidentiality_state() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
  IF NEW."Category"='confidentiality' AND NOT EXISTS(SELECT 1 FROM "PatientConfidentialityStates"
    WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId" AND "Revision">=NEW."Revision"
      AND "Revision"=(SELECT MAX("Revision") FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId" AND "Category"='confidentiality' AND "Key"='chart')
      AND "Restricted"=(SELECT "AlertIsActive" FROM "PatientAlertVersions" WHERE "TenantId"=NEW."TenantId" AND "PatientId"=NEW."PatientId" AND "Category"='confidentiality' AND "Key"='chart' ORDER BY "Revision" DESC LIMIT 1)) THEN
    RAISE EXCEPTION 'Confidentiality evidence and effective state must commit together' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $body$;
CREATE CONSTRAINT TRIGGER patient_confidentiality_state_required AFTER INSERT ON "PatientAlertVersions"
  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION require_patient_confidentiality_state();
INSERT INTO "Permissions" ("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
  VALUES ('8a717e93-adb7-4ef3-9a1c-7a7dc032d6e9','PatientAlerts.View','Read local patient alerts','Current scope and sourced reported evidence',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
         ('630cfda1-f72c-4d7e-9e65-b30c7961c616','PatientAlerts.Manage','Manage sourced local patient alerts','Versioned audited reasons and review policy',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false),
         ('064e15f7-1f38-4283-9200-6c02bdbe512c','PatientRestricted.View','Read confidential local charts','Explicit reviewed grant; no family/emergency sharing',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false)
  ON CONFLICT ("Code") DO NOTHING;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT SELECT,INSERT ON "PatientAlertVersions" TO cureflow_tenant;
  REVOKE UPDATE,DELETE ON "PatientAlertVersions" FROM cureflow_tenant;
  GRANT SELECT,INSERT,UPDATE ON "PatientConfidentialityStates" TO cureflow_tenant;
  REVOKE DELETE ON "PatientConfidentialityStates" FROM cureflow_tenant;
END IF; END $grant$;

-- Install after confidentiality state creation. Predicate reads current persisted
-- authority, never JWT permission strings or caller-supplied clearance booleans.
CREATE FUNCTION public.cureflow_patient_read_allowed(scope_tenant uuid, scope_patient uuid)
RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER
SET search_path=pg_catalog,public,pg_temp AS $body$
 SELECT scope_tenant= NULLIF(current_setting('app.current_tenant_id',true),'')::uuid
   AND (NOT EXISTS (SELECT 1 FROM public."PatientConfidentialityStates" s
     WHERE s."TenantId"=scope_tenant AND s."PatientId"=scope_patient AND s."Restricted" AND s."IsActive" AND NOT s."IsDeleted")
   OR EXISTS (
     SELECT 1 FROM public."Users" u
       JOIN public."UserRoleAssignments" a ON a."TenantId"=u."TenantId" AND a."UserId"=u."Id" AND a."IsActive" AND NOT a."IsDeleted"
       JOIN public."Roles" r ON r."Id"=a."RoleId" AND r."IsActive" AND NOT r."IsDeleted"
       JOIN public."RolePermissions" rp ON rp."RoleId"=r."Id" AND rp."IsActive" AND NOT rp."IsDeleted"
       JOIN public."Permissions" p ON p."Id"=rp."PermissionId" AND p."IsActive" AND NOT p."IsDeleted"
     WHERE u."TenantId"=scope_tenant AND u."Id"=NULLIF(current_setting('app.current_user_id',true),'')::uuid
       AND u."IsActive" AND NOT u."IsDeleted" AND p."Code"='PatientRestricted.View'));
$body$;
REVOKE ALL ON FUNCTION public.cureflow_patient_read_allowed(uuid,uuid) FROM PUBLIC;

-- Restrictive SELECT policies also protect search/count/raw child projections.
-- State itself contains only the minimum binary scope data, preventing recursion.
DO $guard$
DECLARE tbl record; expression text; col text;
BEGIN
 FOR tbl IN SELECT c.oid,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE n.nspname='public' AND c.relkind IN ('r','p')
     AND c.relname<>'PatientConfidentialityStates'
     AND EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attname='TenantId' AND NOT a.attisdropped)
 LOOP
  expression=NULL;
  IF tbl.relname='Patients' THEN
   expression='public.cureflow_patient_read_allowed("TenantId","Id")';
  ELSE
   FOREACH col IN ARRAY ARRAY['PatientId','FirstPatientId','SecondPatientId','SourcePatientId','SurvivorPatientId'] LOOP
    IF EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname=col AND NOT a.attisdropped) THEN
     expression=concat_ws(' AND ',expression,format('(%1$I IS NULL OR public.cureflow_patient_read_allowed("TenantId",%1$I))',col));
    END IF;
   END LOOP;
   IF expression IS NULL AND EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='VisitId' AND NOT a.attisdropped) THEN
    expression=format('EXISTS(SELECT 1 FROM public."Visits" v WHERE v."TenantId"=%1$I."TenantId" AND v."Id"=%1$I."VisitId")',tbl.relname);
   ELSIF expression IS NULL AND EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='PrescriptionId' AND NOT a.attisdropped) THEN
    expression=format('EXISTS(SELECT 1 FROM public."Prescriptions" p WHERE p."TenantId"=%1$I."TenantId" AND p."Id"=%1$I."PrescriptionId")',tbl.relname);
   ELSIF expression IS NULL AND EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='MergeId' AND NOT a.attisdropped) THEN
    expression=format('EXISTS(SELECT 1 FROM public."PatientMerges" m WHERE m."TenantId"=%1$I."TenantId" AND m."Id"=%1$I."MergeId")',tbl.relname);
   ELSIF expression IS NULL AND tbl.relname='DocumentScanEvidence' THEN
    expression='EXISTS(SELECT 1 FROM public."PatientDocuments" d WHERE d."TenantId"="DocumentScanEvidence"."TenantId" AND d."Id"="DocumentScanEvidence"."DocumentId")';
   ELSIF expression IS NULL AND tbl.relname='PrivateDocumentVersions' THEN
    expression='("DocumentKind"=''patient_document'' AND EXISTS(SELECT 1 FROM public."PatientDocuments" d WHERE d."TenantId"="PrivateDocumentVersions"."TenantId" AND d."Id"="PrivateDocumentVersions"."DocumentId"))
      OR ("DocumentKind"=''lab_report'' AND EXISTS(SELECT 1 FROM public."LabReports" l WHERE l."TenantId"="PrivateDocumentVersions"."TenantId" AND l."Id"="PrivateDocumentVersions"."DocumentId"))';
   END IF;
  END IF;
  IF expression IS NOT NULL THEN
   EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY',tbl.relname);
   EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY',tbl.relname);
   EXECUTE format('CREATE POLICY patient_confidentiality_read ON public.%I AS RESTRICTIVE FOR SELECT USING (%s)',tbl.relname,expression);
  END IF;
 END LOOP;
 IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
  GRANT EXECUTE ON FUNCTION public.cureflow_patient_read_allowed(uuid,uuid) TO cureflow_tenant;
 END IF;
END $guard$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261007161122_AddPatientAlertsConfidentiality', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "PatientImportBatches" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "FileSha256" character varying(64) NOT NULL,
    "Format" character varying(8) NOT NULL,
    "FileLength" bigint NOT NULL,
    "SourceObjectKey" character varying(500) NOT NULL,
    "SourceObjectVersion" uuid NOT NULL,
    "MappingJson" text NOT NULL,
    "MappingFingerprint" character varying(64) NOT NULL,
    "ScanEngine" character varying(100) NOT NULL,
    "ScanEngineVersion" character varying(200) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "ParseErrorCode" character varying(100),
    "RowCount" integer NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientImportBatches" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_PatientImportBatches_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_PatientImportBatches_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "PatientImportRows" (
    "Id" uuid NOT NULL,
    "BatchId" uuid NOT NULL,
    "RowNumber" integer NOT NULL,
    "RawValuesJson" text NOT NULL,
    "DraftJson" text,
    "ErrorsJson" text NOT NULL,
    "RepeatedFileRow" boolean NOT NULL,
    "SourceHash" character varying(64) NOT NULL,
    "Decision" character varying(20) NOT NULL,
    "PatientId" uuid,
    "ReviewReason" character varying(500),
    "ReviewedBy" uuid,
    "ReviewedAt" timestamp with time zone,
    "ReviewPolicyVersion" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientImportRows" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientImportRows_PatientImportBatches_TenantId_BatchId" FOREIGN KEY ("TenantId", "BatchId") REFERENCES "PatientImportBatches" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PatientImportRows_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_PatientImportBatches_TenantId_FacilityId_FileSha256_Mapping~" ON "PatientImportBatches" ("TenantId", "FacilityId", "FileSha256", "MappingFingerprint");

CREATE UNIQUE INDEX "IX_PatientImportRows_TenantId_BatchId_RowNumber" ON "PatientImportRows" ("TenantId", "BatchId", "RowNumber");

CREATE INDEX "IX_PatientImportRows_TenantId_PatientId" ON "PatientImportRows" ("TenantId", "PatientId");

ALTER TABLE "PatientImportBatches" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientImportBatches" FORCE ROW LEVEL SECURITY;
ALTER TABLE "PatientImportRows" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientImportRows" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "PatientImportBatches" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY tenant_scope ON "PatientImportRows" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY import_owner ON "PatientImportBatches" AS RESTRICTIVE FOR ALL USING ("CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid) WITH CHECK ("CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid);
CREATE POLICY import_owner ON "PatientImportRows" AS RESTRICTIVE FOR ALL USING ("CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid) WITH CHECK ("CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid);
CREATE POLICY patient_confidentiality_read ON "PatientImportRows" AS RESTRICTIVE FOR SELECT USING ("PatientId" IS NULL OR public.cureflow_patient_read_allowed("TenantId","PatientId"));
ALTER TABLE "PatientImportBatches" ADD CONSTRAINT import_source_shape CHECK (
 "Format" IN ('csv','xls','xlsx') AND "FileLength" BETWEEN 1 AND 8388608 AND "RowCount" BETWEEN 1 AND 5000
 AND "FileSha256" ~ '^[0-9A-F]{64}$' AND "MappingFingerprint" ~ '^[0-9A-F]{64}$'
 AND length("SourceObjectKey") BETWEEN 1 AND 500 AND length("PolicyVersion") BETWEEN 1 AND 100
 AND length("ScanEngine") BETWEEN 1 AND 100 AND length("ScanEngineVersion") BETWEEN 1 AND 200
 AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '87600 hours'
 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "PatientImportRows" ADD CONSTRAINT import_row_shape CHECK (
 "RowNumber" BETWEEN 2 AND 5005 AND "SourceHash" ~ '^[0-9A-F]{64}$' AND length("RawValuesJson")<=1048576
 AND jsonb_typeof("RawValuesJson"::jsonb)='array' AND jsonb_typeof("ErrorsJson"::jsonb)='array'
 AND ("DraftJson" IS NULL OR jsonb_typeof("DraftJson"::jsonb)='object') AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted"
 AND (("Decision"='pending' AND "PatientId" IS NULL AND "ReviewReason" IS NULL AND "ReviewedBy" IS NULL AND "ReviewedAt" IS NULL AND "ReviewPolicyVersion" IS NULL)
 OR ("Decision" IN ('skip','create_new') AND ("Decision"='create_new')=("PatientId" IS NOT NULL)
 AND length(btrim("ReviewReason")) BETWEEN 1 AND 500 AND "ReviewedBy" IS NOT NULL AND "ReviewedAt" IS NOT NULL AND length("ReviewPolicyVersion") BETWEEN 1 AND 100)));
CREATE FUNCTION public.guard_patient_import_batch() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Original import source evidence is immutable' USING ERRCODE='23514'; END IF;
 IF NOT EXISTS(SELECT 1 FROM public."Facilities" f WHERE f."TenantId"=NEW."TenantId" AND f."Id"=NEW."FacilityId" AND f."IsActive" AND NOT f."IsDeleted") THEN
  RAISE EXCEPTION 'Import requires current active facility' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER patient_import_batch_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientImportBatches" FOR EACH ROW EXECUTE FUNCTION public.guard_patient_import_batch();
CREATE FUNCTION public.guard_patient_import_row() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE batch public."PatientImportBatches"%ROWTYPE;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Original import row evidence is retained' USING ERRCODE='23514'; END IF;
 SELECT * INTO batch FROM public."PatientImportBatches" b WHERE b."TenantId"=NEW."TenantId" AND b."Id"=NEW."BatchId";
 IF NOT FOUND OR batch."CreatedBy" IS DISTINCT FROM NEW."CreatedBy" OR batch."ExpiresAt"<=clock_timestamp() THEN
  RAISE EXCEPTION 'Import batch scope or review window invalid' USING ERRCODE='23514';
 END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."Decision"<>'pending' THEN RAISE EXCEPTION 'Import rows start pending' USING ERRCODE='23514'; END IF;
  IF (SELECT count(*) FROM public."PatientImportRows" r WHERE r."TenantId"=NEW."TenantId" AND r."BatchId"=NEW."BatchId")>=batch."RowCount" THEN
   RAISE EXCEPTION 'Import row count exceeds original source' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."Decision"<>'pending' OR NEW."Decision" NOT IN ('create_new','skip')
    OR (to_jsonb(OLD)-ARRAY['Decision','PatientId','ReviewReason','ReviewedBy','ReviewedAt','ReviewPolicyVersion','UpdatedAt','UpdatedBy'])
       IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['Decision','PatientId','ReviewReason','ReviewedBy','ReviewedAt','ReviewPolicyVersion','UpdatedAt','UpdatedBy'])
    OR NEW."ReviewedBy" IS DISTINCT FROM NEW."CreatedBy" OR NEW."UpdatedBy" IS DISTINCT FROM NEW."CreatedBy" THEN
   RAISE EXCEPTION 'Import decision is terminal; original row evidence cannot change' USING ERRCODE='23514';
  END IF;
  IF NEW."Decision"='create_new' AND (NEW."DraftJson" IS NULL OR jsonb_array_length(NEW."ErrorsJson"::jsonb)>0 OR NOT EXISTS(
   SELECT 1 FROM public."Patients" p WHERE p."TenantId"=NEW."TenantId" AND p."Id"=NEW."PatientId" AND p."IsActive" AND NOT p."IsDeleted")) THEN
   RAISE EXCEPTION 'Create requires valid retained row and current patient scope' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER patient_import_row_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientImportRows" FOR EACH ROW EXECUTE FUNCTION public.guard_patient_import_row();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "PatientImportBatches","PatientImportRows" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientImportBatches","PatientImportRows" FROM cureflow_tenant;
 GRANT UPDATE ("Decision","PatientId","ReviewReason","ReviewedBy","ReviewedAt","ReviewPolicyVersion","UpdatedAt","UpdatedBy") ON "PatientImportRows" TO cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008175020_AddReviewedPatientImports', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "ScheduleRules" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "PractitionerUserId" uuid,
    "ResourceId" uuid,
    "OperatingSettingId" uuid NOT NULL,
    "Timezone" character varying(100) NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveThrough" date NOT NULL,
    "WeekdayMask" integer NOT NULL,
    "StartMinute" integer NOT NULL,
    "EndMinute" integer NOT NULL,
    "AmbiguousTimePolicy" character varying(16) NOT NULL,
    "Revision" bigint NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ScheduleRules" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ScheduleRules_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleRules_FacilityOperatingSettings_OperatingSettingId" FOREIGN KEY ("OperatingSettingId") REFERENCES "FacilityOperatingSettings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleRules_FacilityResources_ResourceId" FOREIGN KEY ("ResourceId") REFERENCES "FacilityResources" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleRules_Users_PractitionerUserId" FOREIGN KEY ("PractitionerUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ScheduleRules_OperatingSettingId" ON "ScheduleRules" ("OperatingSettingId");

CREATE INDEX "IX_ScheduleRules_PractitionerUserId" ON "ScheduleRules" ("PractitionerUserId");

CREATE INDEX "IX_ScheduleRules_ResourceId" ON "ScheduleRules" ("ResourceId");

CREATE INDEX "IX_ScheduleRules_TenantId_FacilityId_Id" ON "ScheduleRules" ("TenantId", "FacilityId", "Id");

CREATE INDEX "IX_ScheduleRules_TenantId_PractitionerUserId_EffectiveFrom_Eff~" ON "ScheduleRules" ("TenantId", "PractitionerUserId", "EffectiveFrom", "EffectiveThrough");

CREATE INDEX "IX_ScheduleRules_TenantId_ResourceId_EffectiveFrom_EffectiveTh~" ON "ScheduleRules" ("TenantId", "ResourceId", "EffectiveFrom", "EffectiveThrough");

ALTER TABLE "ScheduleRules" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ScheduleRules" FORCE ROW LEVEL SECURITY;
CREATE POLICY schedule_rule_tenant ON "ScheduleRules"
 USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "ScheduleRules" ADD CONSTRAINT schedule_rule_bounds CHECK (
 ("PractitionerUserId" IS NOT NULL OR "ResourceId" IS NOT NULL) AND "Revision">0 AND NOT "IsDeleted"
 AND "EffectiveThrough">="EffectiveFrom" AND "EffectiveThrough"-"EffectiveFrom"<=365
 AND "WeekdayMask" BETWEEN 1 AND 127 AND "StartMinute" BETWEEN 0 AND 1439
 AND "EndMinute">"StartMinute" AND "EndMinute"<=1440
 AND "AmbiguousTimePolicy" IN ('reject','earlier','later'));
CREATE FUNCTION enforce_schedule_rule() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Retained schedule rules cannot be deleted' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF TG_OP='UPDATE' THEN
  IF (to_jsonb(NEW)-ARRAY['IsActive','Revision','UpdatedAt','UpdatedBy']) IS DISTINCT FROM
     (to_jsonb(OLD)-ARRAY['IsActive','Revision','UpdatedAt','UpdatedBy'])
     OR NOT OLD."IsActive" OR NEW."IsActive" OR NEW."Revision"<>OLD."Revision"+1 THEN
   RAISE EXCEPTION 'Schedule rules are retained; only a versioned disable is permitted' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
 END IF;
 IF NEW."Revision"<>1 OR NOT NEW."IsActive" THEN RAISE EXCEPTION 'Invalid initial schedule revision' USING ERRCODE='23514'; END IF;
 PERFORM "Id" FROM "Facilities" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."FacilityId" AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
 IF NOT FOUND THEN RAISE EXCEPTION 'Active same-tenant facility required' USING ERRCODE='23514'; END IF;
 PERFORM "Id" FROM "FacilityOperatingSettings" s WHERE s."TenantId"=NEW."TenantId" AND s."FacilityId"=NEW."FacilityId"
  AND s."Id"=NEW."OperatingSettingId" AND s."Timezone"=NEW."Timezone"
  AND s."Revision"=(SELECT max("Revision") FROM "FacilityOperatingSettings" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId");
 IF NOT FOUND THEN RAISE EXCEPTION 'Current explicit facility timezone required' USING ERRCODE='23514'; END IF;
 IF NEW."PractitionerUserId" IS NOT NULL THEN
  PERFORM u."Id" FROM "Users" u JOIN "StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
   WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."PractitionerUserId" AND u."Role" IN (1,4,6)
   AND u."IsActive" AND NOT u."IsDeleted" AND p."IsActive" AND p."IsAvailable" AND NOT p."IsDeleted" FOR SHARE OF u,p;
  IF NOT FOUND THEN RAISE EXCEPTION 'Active eligible same-tenant practitioner required' USING ERRCODE='23514'; END IF;
 END IF;
 IF NEW."ResourceId" IS NOT NULL THEN
  PERFORM "Id" FROM "FacilityResources" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Id"=NEW."ResourceId"
   AND "IsActive" AND NOT "IsDeleted" AND "OperationalStatus"='enabled' FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Enabled same-facility resource required' USING ERRCODE='23514'; END IF;
 END IF;
 IF NOT EXISTS(SELECT 1 FROM generate_series(0,least(6,NEW."EffectiveThrough"-NEW."EffectiveFrom")) d
    WHERE (NEW."WeekdayMask" & (1 << extract(dow FROM NEW."EffectiveFrom"+d)::int))<>0) THEN
  RAISE EXCEPTION 'The rule must occur in its effective interval' USING ERRCODE='23514';
 END IF;
 IF EXISTS(SELECT 1 FROM "ScheduleRules" r WHERE r."TenantId"=NEW."TenantId" AND r."IsActive" AND NOT r."IsDeleted"
    AND (r."PractitionerUserId"=NEW."PractitionerUserId" OR r."ResourceId"=NEW."ResourceId")
    AND r."EffectiveFrom"<=NEW."EffectiveThrough" AND r."EffectiveThrough">=NEW."EffectiveFrom"
    AND (r."Timezone"<>NEW."Timezone" OR (r."StartMinute"<NEW."EndMinute" AND r."EndMinute">NEW."StartMinute"
      AND EXISTS(SELECT 1 FROM generate_series(0,least(6,least(r."EffectiveThrough",NEW."EffectiveThrough")-greatest(r."EffectiveFrom",NEW."EffectiveFrom"))) d
       WHERE ((r."WeekdayMask" & NEW."WeekdayMask") & (1 << extract(dow FROM greatest(r."EffectiveFrom",NEW."EffectiveFrom")+d)::int))<>0)))) THEN
  RAISE EXCEPTION 'Overlapping practitioner or resource rules must be resolved explicitly' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $body$;
CREATE TRIGGER schedule_rule_guard BEFORE INSERT OR UPDATE OR DELETE ON "ScheduleRules" FOR EACH ROW EXECUTE FUNCTION enforce_schedule_rule();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "ScheduleRules" TO cureflow_tenant;
 GRANT UPDATE ("IsActive","Revision","UpdatedAt","UpdatedBy") ON "ScheduleRules" TO cureflow_tenant;
 REVOKE DELETE ON "ScheduleRules" FROM cureflow_tenant;
END IF; END $grant$;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008175428_AddFacilityScheduleRules', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "PatientExportJobs" (
    "Id" uuid NOT NULL,
    "RequesterId" uuid NOT NULL,
    "PatientIdsJson" text NOT NULL,
    "PatientTagsJson" text NOT NULL,
    "FieldsJson" text NOT NULL,
    "Purpose" character varying(500) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "State" character varying(20) NOT NULL,
    "Revision" bigint NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "ObjectKey" character varying(500),
    "ObjectVersion" uuid,
    "Sha256" character varying(64),
    "Length" bigint,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_PatientExportJobs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PatientExportJobs_Users_RequesterId" FOREIGN KEY ("RequesterId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_PatientExportJobs_RequesterId" ON "PatientExportJobs" ("RequesterId");

CREATE INDEX "IX_PatientExportJobs_TenantId_RequesterId_CreatedAt" ON "PatientExportJobs" ("TenantId", "RequesterId", "CreatedAt");

ALTER TABLE "PatientExportJobs" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "PatientExportJobs" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "PatientExportJobs" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY export_owner ON "PatientExportJobs" AS RESTRICTIVE FOR SELECT USING (
 NULLIF(current_setting('app.current_user_id',true),'') IS NULL OR "RequesterId"=NULLIF(current_setting('app.current_user_id',true),'')::uuid);
CREATE POLICY patient_confidentiality_read ON "PatientExportJobs" AS RESTRICTIVE FOR SELECT USING (
 NOT EXISTS(SELECT 1 FROM jsonb_array_elements_text("PatientIdsJson"::jsonb) id WHERE NOT public.cureflow_patient_read_allowed("TenantId",id.value::uuid)));
ALTER TABLE "PatientExportJobs" ADD CONSTRAINT export_shape CHECK (COALESCE(
 jsonb_typeof("PatientIdsJson"::jsonb)='array' AND jsonb_array_length("PatientIdsJson"::jsonb) BETWEEN 1 AND 200
 AND jsonb_typeof("PatientTagsJson"::jsonb)='object' AND jsonb_typeof("FieldsJson"::jsonb)='array' AND jsonb_array_length("FieldsJson"::jsonb) BETWEEN 1 AND 6
 AND length("PatientIdsJson")<=10000 AND length("PatientTagsJson")<=40000 AND length("FieldsJson")<=200
 AND length(btrim("Purpose")) BETWEEN 1 AND 500 AND length("PolicyVersion") BETWEEN 1 AND 100
 AND "CreatedBy"="RequesterId" AND "ExpiresAt">"CreatedAt" AND "ExpiresAt"<="CreatedAt"+interval '24 hours' AND "IsActive" AND NOT "IsDeleted"
 AND (("State"='pending' AND "Revision"=1 AND "ObjectKey" IS NULL AND "ObjectVersion" IS NULL AND "Sha256" IS NULL AND "Length" IS NULL)
  OR ("State"='ready' AND "Revision"=2 AND length("ObjectKey") BETWEEN 1 AND 500 AND "ObjectVersion" IS NOT NULL AND "Sha256" ~ '^[0-9A-F]{64}$' AND "Length" BETWEEN 1 AND 2097152)),false));
CREATE FUNCTION public.guard_patient_export_job() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE selected uuid; field text;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Export receipt/provenance is retained' USING ERRCODE='23514'; END IF;
 IF NOT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."RequesterId" AND u."IsActive" AND NOT u."IsDeleted")
  OR NEW."ExpiresAt"<=clock_timestamp() THEN RAISE EXCEPTION 'Export requester/review window invalid' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."State"<>'pending' OR NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid THEN
   RAISE EXCEPTION 'Export requires authenticated requester and pending state' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."State"<>'pending' OR NEW."State"<>'ready' OR NEW."Revision"<>2
   OR (to_jsonb(OLD)-ARRAY['State','Revision','ObjectKey','ObjectVersion','Sha256','Length','UpdatedAt'])
      IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','ObjectKey','ObjectVersion','Sha256','Length','UpdatedAt']) THEN
   RAISE EXCEPTION 'Export source is immutable and artifact commit is terminal' USING ERRCODE='23514';
  END IF;
 END IF;
 IF (SELECT count(*) FROM jsonb_array_elements_text(NEW."PatientIdsJson"::jsonb))<>
    (SELECT count(DISTINCT value) FROM jsonb_array_elements_text(NEW."PatientIdsJson"::jsonb)) THEN
  RAISE EXCEPTION 'Export scope cannot repeat patients' USING ERRCODE='23514';
 END IF;
 FOR selected IN SELECT value::uuid FROM jsonb_array_elements_text(NEW."PatientIdsJson"::jsonb) LOOP
  IF NOT EXISTS(SELECT 1 FROM public."Patients" p WHERE p."TenantId"=NEW."TenantId" AND p."Id"=selected AND p."IsActive" AND NOT p."IsDeleted"
    AND NOT EXISTS(SELECT 1 FROM public."PatientConfidentialityStates" s WHERE s."TenantId"=p."TenantId" AND s."PatientId"=p."Id" AND s."Restricted"))
    OR NOT (NEW."PatientTagsJson"::jsonb ? selected::text) THEN
   RAISE EXCEPTION 'Export patient scope is not current unrestricted scope' USING ERRCODE='23514';
  END IF;
 END LOOP;
 FOR field IN SELECT value FROM jsonb_array_elements_text(NEW."FieldsJson"::jsonb) LOOP
  IF field NOT IN ('patient_id','name','phone','email','age','gender') THEN RAISE EXCEPTION 'Unsupported directory export field' USING ERRCODE='23514'; END IF;
 END LOOP;
 RETURN NEW;
END $guard$;
CREATE TRIGGER patient_export_job_guard BEFORE INSERT OR UPDATE OR DELETE ON "PatientExportJobs" FOR EACH ROW EXECUTE FUNCTION public.guard_patient_export_job();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "PatientExportJobs" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "PatientExportJobs" FROM cureflow_tenant;
 GRANT UPDATE ("State","Revision","ObjectKey","ObjectVersion","Sha256","Length","UpdatedAt") ON "PatientExportJobs" TO cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008180009_AddPatientDirectoryExports', '10.0.0');

COMMIT;

START TRANSACTION;
-- Additive correction: hospital metadata reads must not confer facility mutation rights.
ALTER TABLE public."Facilities" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."Facilities" FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS facilities_read_scope ON public."Facilities";
DROP POLICY IF EXISTS facilities_write_scope ON public."Facilities";
CREATE POLICY facilities_read_scope ON public."Facilities" FOR SELECT USING (
 "TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid
 OR (current_setting('app.platform_admin',true)='true' AND pg_has_role(current_user,'cureflow_control_plane','MEMBER')));
CREATE POLICY facilities_write_scope ON public."Facilities" FOR ALL USING (
 current_setting('app.platform_admin',true)='true' AND pg_has_role(current_user,'cureflow_control_plane','MEMBER'))
 WITH CHECK (current_setting('app.platform_admin',true)='true' AND pg_has_role(current_user,'cureflow_control_plane','MEMBER'));
CREATE OR REPLACE FUNCTION public.cureflow_lock_active_facility(scope_tenant uuid,scope_facility uuid)
 RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public,pg_temp AS $body$
DECLARE found_id uuid;
BEGIN
 IF scope_tenant IS DISTINCT FROM NULLIF(current_setting('app.current_tenant_id',true),'')::uuid THEN RETURN NULL; END IF;
 SELECT "Id" INTO found_id FROM public."Facilities" WHERE "TenantId"=scope_tenant AND "Id"=scope_facility
   AND "IsActive" AND NOT "IsDeleted" FOR SHARE;
 RETURN found_id;
END $body$;
REVOKE ALL ON FUNCTION public.cureflow_lock_active_facility(uuid,uuid) FROM PUBLIC;
-- Preserve trigger definitions and their other checks; replace only the reviewed facility lock statement.
DO $repair$ DECLARE function_row record; definition text; repaired text; BEGIN
 FOR function_row IN SELECT p.oid FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
  WHERE n.nspname='public' AND p.proname=ANY(ARRAY['enforce_organization_node','validate_operating_settings_append',
    'enforce_facility_resource','enforce_identifier_counter','validate_identifier_allocation','enforce_schedule_rule'])
 LOOP
  definition=pg_get_functiondef(function_row.oid);
  repaired=regexp_replace(definition,'(PERFORM "Id" FROM "Facilities"[^;]*)( FOR SHARE);',
   '\1 AND public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId")=NEW."FacilityId";','g');
  IF repaired=definition THEN RAISE EXCEPTION 'Expected facility guard lock statement was not found'; END IF;
  EXECUTE repaired;
 END LOOP;
END $repair$;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT ON public."Facilities" TO cureflow_tenant;
 REVOKE INSERT,UPDATE,DELETE ON public."Facilities" FROM cureflow_tenant;
 GRANT EXECUTE ON FUNCTION public.cureflow_lock_active_facility(uuid,uuid) TO cureflow_tenant;
END IF; END $grant$;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008180640_AddFacilityReadBoundary', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "ReferringDoctors" ADD CONSTRAINT "AK_ReferringDoctors_TenantId_Id" UNIQUE ("TenantId", "Id");

ALTER TABLE "Leads" ADD CONSTRAINT "AK_Leads_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "LeadConversions" (
    "Id" uuid NOT NULL,
    "LeadId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "Decision" character varying(20) NOT NULL,
    "SourceLeadTag" character varying(64) NOT NULL,
    "OriginalLeadJson" text NOT NULL,
    "CandidateReviewTag" character varying(64) NOT NULL,
    "SelectedPatientTag" character varying(200),
    "EvidenceReference" character varying(200) NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "ReferralSource" character varying(200),
    "ReferringDoctorId" uuid,
    "PolicyVersion" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_LeadConversions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_LeadConversions_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_LeadConversions_Leads_TenantId_LeadId" FOREIGN KEY ("TenantId", "LeadId") REFERENCES "Leads" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_LeadConversions_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_LeadConversions_ReferringDoctors_TenantId_ReferringDoctorId" FOREIGN KEY ("TenantId", "ReferringDoctorId") REFERENCES "ReferringDoctors" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_LeadConversions_TenantId_FacilityId" ON "LeadConversions" ("TenantId", "FacilityId");

CREATE UNIQUE INDEX "IX_LeadConversions_TenantId_LeadId" ON "LeadConversions" ("TenantId", "LeadId");

CREATE INDEX "IX_LeadConversions_TenantId_PatientId" ON "LeadConversions" ("TenantId", "PatientId");

CREATE INDEX "IX_LeadConversions_TenantId_ReferringDoctorId" ON "LeadConversions" ("TenantId", "ReferringDoctorId");

ALTER TABLE "Leads" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Leads" FORCE ROW LEVEL SECURITY;
DO $policy$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_policy WHERE polrelid='public."Leads"'::regclass AND polname='conversion_tenant_scope') THEN
 CREATE POLICY conversion_tenant_scope ON "Leads" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
END IF; END $policy$;
CREATE POLICY conversion_tenant_guard ON "Leads" AS RESTRICTIVE FOR ALL USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY converted_patient_confidentiality ON "Leads" AS RESTRICTIVE FOR SELECT USING ("ConvertedPatientId" IS NULL OR public.cureflow_patient_read_allowed("TenantId","ConvertedPatientId"));
ALTER TABLE "LeadConversions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "LeadConversions" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "LeadConversions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_confidentiality_read ON "LeadConversions" AS RESTRICTIVE FOR SELECT USING (public.cureflow_patient_read_allowed("TenantId","PatientId"));
ALTER TABLE "LeadConversions" ADD CONSTRAINT lead_conversion_shape CHECK (COALESCE(
 "Decision" IN ('create_new','link_existing') AND "SourceLeadTag" ~ '^[0-9A-F]{64}$' AND "CandidateReviewTag" ~ '^[0-9A-F]{64}$'
 AND jsonb_typeof("OriginalLeadJson"::jsonb)='object' AND length("OriginalLeadJson")<=32768
 AND length(btrim("EvidenceReference")) BETWEEN 1 AND 200 AND length(btrim("Reason")) BETWEEN 1 AND 500
 AND length("PolicyVersion") BETWEEN 1 AND 100 AND ("Decision"='link_existing')=("SelectedPatientTag" IS NOT NULL)
 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted",false));
CREATE FUNCTION public.guard_lead_conversion() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE source public."Leads"%ROWTYPE;
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Lead conversion provenance is immutable' USING ERRCODE='23514'; END IF;
 SELECT * INTO source FROM public."Leads" l WHERE l."TenantId"=NEW."TenantId" AND l."Id"=NEW."LeadId" FOR UPDATE;
 IF NOT FOUND OR source."ConvertedAt" IS NOT NULL OR NOT source."IsActive" OR source."IsDeleted" THEN
  RAISE EXCEPTION 'Conversion requires current active unconverted lead' USING ERRCODE='23514';
 END IF;
 IF NEW."OriginalLeadJson"::jsonb->>'Id' IS DISTINCT FROM NEW."LeadId"::text
  OR NEW."OriginalLeadJson"::jsonb->>'TenantId' IS DISTINCT FROM NEW."TenantId"::text
  OR NEW."OriginalLeadJson"::jsonb->>'Name' IS DISTINCT FROM source."Name"
  OR NEW."OriginalLeadJson"::jsonb->>'Phone' IS DISTINCT FROM source."Phone"
  OR (NEW."OriginalLeadJson"::jsonb->>'Age')::integer IS DISTINCT FROM source."Age"
  OR (NEW."OriginalLeadJson"::jsonb->>'Gender')::integer IS DISTINCT FROM source."Gender" THEN
  RAISE EXCEPTION 'Original lead identity evidence differs' USING ERRCODE='23514';
 END IF;
 IF public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
  OR NOT EXISTS(SELECT 1 FROM public."Patients" p WHERE p."TenantId"=NEW."TenantId" AND p."Id"=NEW."PatientId" AND p."IsActive" AND NOT p."IsDeleted")
  OR (NEW."ReferringDoctorId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM public."ReferringDoctors" d WHERE d."TenantId"=NEW."TenantId" AND d."Id"=NEW."ReferringDoctorId" AND d."IsActive" AND NOT d."IsDeleted")) THEN
  RAISE EXCEPTION 'Conversion target/facility/referral scope is not current' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER lead_conversion_guard BEFORE INSERT OR UPDATE OR DELETE ON "LeadConversions" FOR EACH ROW EXECUTE FUNCTION public.guard_lead_conversion();
CREATE FUNCTION public.require_lead_conversion_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM public."Leads" l WHERE l."TenantId"=NEW."TenantId" AND l."Id"=NEW."LeadId"
   AND l."ConvertedPatientId"=NEW."PatientId" AND l."ConvertedAt" IS NOT NULL AND NOT l."IsActive") THEN
  RAISE EXCEPTION 'Conversion source/target mapping must commit together' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER lead_conversion_commit AFTER INSERT ON "LeadConversions" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_lead_conversion_commit();
CREATE FUNCTION public.guard_retained_lead_conversion() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF OLD."ConvertedAt" IS NOT NULL AND (TG_OP='DELETE' OR
  (to_jsonb(OLD)-ARRAY['UpdatedAt','UpdatedBy']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['UpdatedAt','UpdatedBy'])) THEN
  RAISE EXCEPTION 'Converted lead source and mapping are retained' USING ERRCODE='23514';
 END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER retained_lead_conversion BEFORE UPDATE OR DELETE ON "Leads" FOR EACH ROW EXECUTE FUNCTION public.guard_retained_lead_conversion();
CREATE FUNCTION public.require_new_lead_mapping() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF OLD."ConvertedAt" IS NULL AND NEW."ConvertedAt" IS NOT NULL AND NOT EXISTS(
  SELECT 1 FROM public."LeadConversions" c WHERE c."TenantId"=NEW."TenantId" AND c."LeadId"=NEW."Id" AND c."PatientId"=NEW."ConvertedPatientId") THEN
  RAISE EXCEPTION 'New conversion requires retained reviewed mapping' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER new_lead_mapping AFTER UPDATE ON "Leads" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_new_lead_mapping();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "LeadConversions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "LeadConversions" FROM cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008181146_AddReviewedLeadConversions', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "ScheduleExceptions" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "PractitionerUserId" uuid,
    "ResourceId" uuid,
    "OperatingSettingId" uuid NOT NULL,
    "Timezone" character varying(100) NOT NULL,
    "Kind" character varying(16) NOT NULL,
    "StartsAtUtc" timestamp with time zone NOT NULL,
    "EndsAtUtc" timestamp with time zone NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "Revision" bigint NOT NULL DEFAULT 1,
    "DisabledAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ScheduleExceptions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ScheduleExceptions_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleExceptions_FacilityOperatingSettings_OperatingSetti~" FOREIGN KEY ("OperatingSettingId") REFERENCES "FacilityOperatingSettings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleExceptions_FacilityResources_ResourceId" FOREIGN KEY ("ResourceId") REFERENCES "FacilityResources" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ScheduleExceptions_Users_PractitionerUserId" FOREIGN KEY ("PractitionerUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "AppointmentRescheduleWorks" (
    "Id" uuid NOT NULL,
    "ExceptionId" uuid NOT NULL,
    "AppointmentId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "OriginalScheduledAtUtc" timestamp with time zone NOT NULL,
    "OriginalDurationMinutes" integer NOT NULL,
    "FacilityAssociationNeedsReview" boolean NOT NULL,
    "State" character varying(32) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentRescheduleWorks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppointmentRescheduleWorks_Appointments_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "Appointments" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentRescheduleWorks_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentRescheduleWorks_ScheduleExceptions_ExceptionId" FOREIGN KEY ("ExceptionId") REFERENCES "ScheduleExceptions" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_AppointmentRescheduleWorks_AppointmentId" ON "AppointmentRescheduleWorks" ("AppointmentId");

CREATE INDEX "IX_AppointmentRescheduleWorks_ExceptionId" ON "AppointmentRescheduleWorks" ("ExceptionId");

CREATE UNIQUE INDEX "IX_AppointmentRescheduleWorks_TenantId_ExceptionId_Appointment~" ON "AppointmentRescheduleWorks" ("TenantId", "ExceptionId", "AppointmentId");

CREATE INDEX "IX_AppointmentRescheduleWorks_TenantId_ExceptionId_Id" ON "AppointmentRescheduleWorks" ("TenantId", "ExceptionId", "Id");

CREATE INDEX "IX_AppointmentRescheduleWorks_TenantId_PatientId" ON "AppointmentRescheduleWorks" ("TenantId", "PatientId");

CREATE INDEX "IX_ScheduleExceptions_OperatingSettingId" ON "ScheduleExceptions" ("OperatingSettingId");

CREATE INDEX "IX_ScheduleExceptions_PractitionerUserId" ON "ScheduleExceptions" ("PractitionerUserId");

CREATE INDEX "IX_ScheduleExceptions_ResourceId" ON "ScheduleExceptions" ("ResourceId");

CREATE INDEX "IX_ScheduleExceptions_TenantId_FacilityId_Id" ON "ScheduleExceptions" ("TenantId", "FacilityId", "Id");

CREATE INDEX "IX_ScheduleExceptions_TenantId_FacilityId_StartsAtUtc_EndsAtUtc" ON "ScheduleExceptions" ("TenantId", "FacilityId", "StartsAtUtc", "EndsAtUtc");

ALTER TABLE "ScheduleExceptions" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ScheduleExceptions" FORCE ROW LEVEL SECURITY;
CREATE POLICY schedule_exception_tenant ON "ScheduleExceptions" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "ScheduleExceptions" ADD CONSTRAINT schedule_exception_bounds CHECK (
 "Revision">0 AND NOT "IsDeleted" AND "Kind" IN ('leave','holiday','block')
 AND ("Kind"<>'leave' OR "PractitionerUserId" IS NOT NULL)
 AND ("Kind"<>'holiday' OR ("PractitionerUserId" IS NULL AND "ResourceId" IS NULL))
 AND "EndsAtUtc">"StartsAtUtc" AND "EndsAtUtc"-"StartsAtUtc"<=interval '31 days'
 AND length(btrim("Reason")) BETWEEN 3 AND 500
 AND (("IsActive" AND "DisabledAt" IS NULL) OR (NOT "IsActive" AND "DisabledAt" IS NOT NULL)));
CREATE FUNCTION enforce_schedule_exception() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Schedule exceptions are retained' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF TG_OP='UPDATE' THEN
  IF (to_jsonb(NEW)-ARRAY['IsActive','Revision','DisabledAt','UpdatedAt','UpdatedBy']) IS DISTINCT FROM
     (to_jsonb(OLD)-ARRAY['IsActive','Revision','DisabledAt','UpdatedAt','UpdatedBy'])
   OR NOT OLD."IsActive" OR NEW."IsActive" OR NEW."Revision"<>OLD."Revision"+1 OR NEW."DisabledAt" IS NULL THEN
   RAISE EXCEPTION 'Only a versioned retained exception disable is permitted' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
 END IF;
 IF NEW."Revision"<>1 OR NOT NEW."IsActive" OR NEW."DisabledAt" IS NOT NULL
   OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL THEN
  RAISE EXCEPTION 'Active same-tenant facility and initial exception version required' USING ERRCODE='23514';
 END IF;
 PERFORM "Id" FROM "FacilityOperatingSettings" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId"
  AND "Id"=NEW."OperatingSettingId" AND "Timezone"=NEW."Timezone"
  AND "Revision"=(SELECT max("Revision") FROM "FacilityOperatingSettings" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId");
 IF NOT FOUND THEN RAISE EXCEPTION 'Current explicit facility timezone required' USING ERRCODE='23514'; END IF;
 IF NEW."PractitionerUserId" IS NOT NULL THEN
  PERFORM u."Id" FROM "Users" u JOIN "StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
   WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."PractitionerUserId" AND u."Role" IN (1,4,6)
   AND NOT u."IsDeleted" AND NOT p."IsDeleted" FOR SHARE OF u,p;
  IF NOT FOUND THEN RAISE EXCEPTION 'Same-tenant practitioner required' USING ERRCODE='23514'; END IF;
 END IF;
 IF NEW."ResourceId" IS NOT NULL THEN
  PERFORM "Id" FROM "FacilityResources" WHERE "TenantId"=NEW."TenantId" AND "FacilityId"=NEW."FacilityId" AND "Id"=NEW."ResourceId" AND NOT "IsDeleted" FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Same-facility resource required' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $body$;
CREATE TRIGGER schedule_exception_guard BEFORE INSERT OR UPDATE OR DELETE ON "ScheduleExceptions" FOR EACH ROW EXECUTE FUNCTION enforce_schedule_exception();
ALTER TABLE "AppointmentRescheduleWorks" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentRescheduleWorks" FORCE ROW LEVEL SECURITY;
CREATE POLICY reschedule_work_tenant ON "AppointmentRescheduleWorks" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY reschedule_work_patient_scope ON "AppointmentRescheduleWorks" AS RESTRICTIVE FOR SELECT
 USING (public.cureflow_patient_read_allowed("TenantId","PatientId"));
CREATE FUNCTION enforce_reschedule_work() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE exception_row "ScheduleExceptions"; appointment_row "Appointments";
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Original potential booking impact is immutable' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF NEW."IsDeleted" OR NOT NEW."IsActive" OR NEW."State"<>'needs_review' OR NOT NEW."FacilityAssociationNeedsReview" THEN
  RAISE EXCEPTION 'Invalid initial booking impact evidence' USING ERRCODE='23514'; END IF;
 SELECT * INTO exception_row FROM "ScheduleExceptions" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."ExceptionId" AND "IsActive";
 IF NOT FOUND THEN RAISE EXCEPTION 'Current same-tenant exception required' USING ERRCODE='23514'; END IF;
 SELECT * INTO appointment_row FROM "Appointments" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."AppointmentId"
  AND "IsActive" AND NOT "IsDeleted" AND "Status" IN (0,1) FOR SHARE;
 IF NOT FOUND OR appointment_row."PatientId"<>NEW."PatientId" OR appointment_row."ScheduledAt"<>NEW."OriginalScheduledAtUtc"
  OR appointment_row."DurationMinutes"<>NEW."OriginalDurationMinutes" OR NEW."OriginalDurationMinutes" NOT BETWEEN 1 AND 1440
  OR (exception_row."PractitionerUserId" IS NOT NULL AND appointment_row."DoctorUserId" IS DISTINCT FROM exception_row."PractitionerUserId")
  OR (exception_row."ResourceId" IS NOT NULL AND exception_row."PractitionerUserId" IS NULL)
  OR appointment_row."ScheduledAt">=exception_row."EndsAtUtc"
  OR appointment_row."ScheduledAt"+make_interval(mins=>appointment_row."DurationMinutes")<=exception_row."StartsAtUtc" THEN
  RAISE EXCEPTION 'Impact must retain the exact overlapping source booking' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $body$;
CREATE TRIGGER reschedule_work_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentRescheduleWorks" FOR EACH ROW EXECUTE FUNCTION enforce_reschedule_work();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "ScheduleExceptions","AppointmentRescheduleWorks" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "ScheduleExceptions","AppointmentRescheduleWorks" FROM cureflow_tenant;
 GRANT UPDATE ("IsActive","Revision","DisabledAt","UpdatedAt","UpdatedBy") ON "ScheduleExceptions" TO cureflow_tenant;
END IF; END $grant$;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008182413_AddScheduleExceptions', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Users" ADD CONSTRAINT "AK_Users_TenantId_Id" UNIQUE ("TenantId", "Id");

ALTER TABLE "Appointments" ADD CONSTRAINT "AK_Appointments_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "AppointmentReservations" (
    "Id" uuid NOT NULL,
    "OwnerUserId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "PractitionerUserId" uuid NOT NULL,
    "StartsAtUtc" timestamp with time zone NOT NULL,
    "EndsAtUtc" timestamp with time zone NOT NULL,
    "HoldExpiresAtUtc" timestamp with time zone NOT NULL,
    "State" character varying(20) NOT NULL,
    "Revision" bigint NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "AppointmentId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentReservations" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_AppointmentReservations_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_AppointmentReservations_Appointments_TenantId_AppointmentId" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentReservations_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentReservations_Users_TenantId_OwnerUserId" FOREIGN KEY ("TenantId", "OwnerUserId") REFERENCES "Users" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentReservations_Users_TenantId_PractitionerUserId" FOREIGN KEY ("TenantId", "PractitionerUserId") REFERENCES "Users" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "AppointmentReservationSeats" (
    "Id" uuid NOT NULL,
    "ReservationId" uuid NOT NULL,
    "TargetType" character varying(20) NOT NULL,
    "TargetId" uuid NOT NULL,
    "Seat" integer NOT NULL,
    "CapacitySnapshot" integer NOT NULL,
    "StartsAtUtc" timestamp with time zone NOT NULL,
    "EndsAtUtc" timestamp with time zone NOT NULL,
    "State" character varying(20) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentReservationSeats" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppointmentReservationSeats_AppointmentReservations_TenantI~" FOREIGN KEY ("TenantId", "ReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_AppointmentReservations_TenantId_AppointmentId" ON "AppointmentReservations" ("TenantId", "AppointmentId") WHERE "AppointmentId" IS NOT NULL;

CREATE INDEX "IX_AppointmentReservations_TenantId_FacilityId" ON "AppointmentReservations" ("TenantId", "FacilityId");

CREATE INDEX "IX_AppointmentReservations_TenantId_OwnerUserId_State_HoldExpi~" ON "AppointmentReservations" ("TenantId", "OwnerUserId", "State", "HoldExpiresAtUtc");

CREATE INDEX "IX_AppointmentReservations_TenantId_PractitionerUserId" ON "AppointmentReservations" ("TenantId", "PractitionerUserId");

CREATE UNIQUE INDEX "IX_AppointmentReservationSeats_TenantId_ReservationId_TargetTy~" ON "AppointmentReservationSeats" ("TenantId", "ReservationId", "TargetType", "TargetId");

CREATE INDEX "IX_AppointmentReservationSeats_TenantId_TargetType_TargetId_St~" ON "AppointmentReservationSeats" ("TenantId", "TargetType", "TargetId", "StartsAtUtc", "EndsAtUtc");

CREATE EXTENSION IF NOT EXISTS btree_gist;
ALTER TABLE "AppointmentReservations" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentReservations" FORCE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentReservationSeats" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentReservationSeats" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "AppointmentReservations" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY tenant_scope ON "AppointmentReservationSeats" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY confirmed_patient_confidentiality ON "AppointmentReservations" AS RESTRICTIVE FOR SELECT USING ("AppointmentId" IS NULL OR EXISTS(
 SELECT 1 FROM public."Appointments" a WHERE a."TenantId"="AppointmentReservations"."TenantId" AND a."Id"="AppointmentReservations"."AppointmentId" AND NOT a."IsDeleted"));
ALTER TABLE "AppointmentReservations" ADD CONSTRAINT reservation_shape CHECK (COALESCE(
 "StartsAtUtc"<"EndsAtUtc" AND "EndsAtUtc"<="StartsAtUtc"+interval '8 hours' AND "HoldExpiresAtUtc">"CreatedAt"
 AND "HoldExpiresAtUtc"<="CreatedAt"+interval '30 minutes' AND "HoldExpiresAtUtc"<="EndsAtUtc"
 AND length("PolicyVersion") BETWEEN 1 AND 100 AND "CreatedBy"="OwnerUserId" AND "IsActive" AND NOT "IsDeleted"
 AND (("State"='held' AND "Revision"=1 AND "AppointmentId" IS NULL)
  OR ("State" IN ('expired','released') AND "Revision"=2 AND "AppointmentId" IS NULL)
  OR ("State"='confirmed' AND "Revision"=2 AND "AppointmentId" IS NOT NULL)),false));
ALTER TABLE "AppointmentReservationSeats" ADD CONSTRAINT reservation_seat_shape CHECK (
 "TargetType" IN ('practitioner','resource') AND "CapacitySnapshot" BETWEEN 1 AND 100000 AND "Seat" BETWEEN 1 AND "CapacitySnapshot"
 AND ("TargetType"<>'practitioner' OR ("Seat"=1 AND "CapacitySnapshot"=1))
 AND "StartsAtUtc"<"EndsAtUtc" AND "State" IN ('held','confirmed','released','expired') AND "IsActive" AND NOT "IsDeleted");
ALTER TABLE "AppointmentReservationSeats" ADD CONSTRAINT reservation_seat_no_overlap EXCLUDE USING gist
 ("TenantId" WITH =,"TargetType" WITH =,"TargetId" WITH =,"Seat" WITH =,tstzrange("StartsAtUtc","EndsAtUtc",'[)') WITH &&)
 WHERE ("State" IN ('held','confirmed'));
CREATE FUNCTION public.guard_appointment_reservation() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Reservation history is retained' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."State"<>'held' OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
   OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
   OR NOT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."OwnerUserId" AND u."IsActive" AND NOT u."IsDeleted") THEN
   RAISE EXCEPTION 'Hold requires current owner and active facility' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."State"<>'held' OR NEW."State" NOT IN ('confirmed','released','expired') OR NEW."Revision"<>OLD."Revision"+1
   OR (to_jsonb(OLD)-ARRAY['State','Revision','AppointmentId','UpdatedAt'])
      IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) THEN
   RAISE EXCEPTION 'Reservation source is immutable; transition is terminal' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='expired' THEN
   IF OLD."HoldExpiresAtUtc">clock_timestamp() THEN RAISE EXCEPTION 'Live hold cannot expire' USING ERRCODE='23514'; END IF;
  ELSIF NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid THEN
   RAISE EXCEPTION 'Reservation owner is required' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='confirmed' AND (OLD."HoldExpiresAtUtc"<=clock_timestamp() OR NOT EXISTS(
   SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId"
    AND a."CreatedAt">=OLD."CreatedAt" AND a."DoctorUserId"=NEW."PractitionerUserId" AND a."ScheduledAt"=NEW."StartsAtUtc"
    AND a."DurationMinutes"=EXTRACT(EPOCH FROM (NEW."EndsAtUtc"-NEW."StartsAtUtc"))/60 AND a."Status" IN (0,1) AND NOT a."IsDeleted")) THEN
   RAISE EXCEPTION 'Expired hold or mismatched new appointment cannot confirm' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_reservation_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentReservations" FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_reservation();
CREATE FUNCTION public.guard_appointment_reservation_seat() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE reservation public."AppointmentReservations"%ROWTYPE; capacity integer;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Reservation occupancy history is retained' USING ERRCODE='23514'; END IF;
 SELECT * INTO reservation FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReservationId";
 IF NOT FOUND OR NEW."StartsAtUtc"<>reservation."StartsAtUtc" OR NEW."EndsAtUtc"<>reservation."EndsAtUtc" OR NEW."State"<>reservation."State" THEN
  RAISE EXCEPTION 'Occupancy must match exact source reservation' USING ERRCODE='23514';
 END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."State"<>'held' THEN RAISE EXCEPTION 'Occupancy starts held' USING ERRCODE='23514'; END IF;
  PERFORM pg_advisory_xact_lock(734291);
  PERFORM pg_advisory_xact_lock(hashtextextended('appointment-seat:'||NEW."TenantId"::text||':'||NEW."TargetType"||':'||NEW."TargetId"::text,0));
  IF NEW."TargetType"='practitioner' THEN
   capacity=1;
   IF NEW."TargetId"<>reservation."PractitionerUserId" OR NOT EXISTS(
    SELECT 1 FROM public."Users" u JOIN public."StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
     WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."TargetId" AND u."IsActive" AND NOT u."IsDeleted"
      AND p."IsActive" AND NOT p."IsDeleted" AND p."IsAvailable") THEN RAISE EXCEPTION 'Practitioner scope unavailable' USING ERRCODE='23514'; END IF;
  ELSE
   SELECT r."Capacity" INTO capacity FROM public."FacilityResources" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."TargetId"
    AND r."FacilityId"=reservation."FacilityId" AND r."ResourceType" IN ('room','equipment') AND r."IsActive" AND NOT r."IsDeleted"
    AND r."OperationalStatus"='enabled' AND r."EffectiveFrom"<=NEW."StartsAtUtc" AND (r."EffectiveUntil" IS NULL OR r."EffectiveUntil">=NEW."EndsAtUtc") FOR SHARE;
  END IF;
  IF capacity IS NULL OR NEW."CapacitySnapshot"<>capacity OR NEW."Seat">capacity THEN
   RAISE EXCEPTION 'Seat is outside current resource capacity' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."State"<>'held' OR NEW."State" NOT IN ('confirmed','released','expired')
   OR (to_jsonb(OLD)-ARRAY['State','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','UpdatedAt']) THEN
   RAISE EXCEPTION 'Occupancy identity/range cannot change; transition is terminal' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_reservation_seat_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentReservationSeats" FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_reservation_seat();
CREATE FUNCTION public.require_reservation_occupancy_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE reservation_id uuid; scope_tenant uuid; current_state text; practitioner uuid;
BEGIN
 IF TG_TABLE_NAME='AppointmentReservations' THEN reservation_id=NEW."Id"; ELSE reservation_id=NEW."ReservationId"; END IF;
 scope_tenant=NEW."TenantId";
 SELECT r."State",r."PractitionerUserId" INTO current_state,practitioner FROM public."AppointmentReservations" r WHERE r."TenantId"=scope_tenant AND r."Id"=reservation_id;
 IF current_state IS NULL OR NOT EXISTS(SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=scope_tenant AND s."ReservationId"=reservation_id AND s."TargetType"='practitioner' AND s."TargetId"=practitioner)
  OR EXISTS(SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=scope_tenant AND s."ReservationId"=reservation_id AND s."State"<>current_state)
  OR (SELECT count(*) FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=scope_tenant AND s."ReservationId"=reservation_id)>6 THEN
   RAISE EXCEPTION 'Reservation and all bounded occupancy seats must commit together' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER reservation_occupancy_commit AFTER INSERT OR UPDATE ON "AppointmentReservations" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_reservation_occupancy_commit();
CREATE CONSTRAINT TRIGGER reservation_seat_commit AFTER INSERT OR UPDATE ON "AppointmentReservationSeats" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_reservation_occupancy_commit();
CREATE FUNCTION public.guard_reserved_resource_capacity() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF NEW."Capacity"<OLD."Capacity" AND EXISTS(SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId"
  AND s."TargetType"='resource' AND s."TargetId"=NEW."Id" AND s."State" IN ('held','confirmed') AND s."EndsAtUtc">clock_timestamp() AND s."Seat">NEW."Capacity") THEN
  RAISE EXCEPTION 'Capacity reduction requires reviewed reservation reconciliation' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER reserved_resource_capacity BEFORE UPDATE ON "FacilityResources" FOR EACH ROW EXECUTE FUNCTION public.guard_reserved_resource_capacity();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "AppointmentReservations","AppointmentReservationSeats" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "AppointmentReservations","AppointmentReservationSeats" FROM cureflow_tenant;
 GRANT UPDATE ("State","Revision","AppointmentId","UpdatedAt") ON "AppointmentReservations" TO cureflow_tenant;
 GRANT UPDATE ("State","UpdatedAt") ON "AppointmentReservationSeats" TO cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008184110_AddAtomicAppointmentReservations', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "Appointments" ADD "BookingKind" character varying(20);

ALTER TABLE "Appointments" ADD "BookingPolicyVersion" character varying(100);

ALTER TABLE "Appointments" ADD "FacilityId" uuid;

ALTER TABLE "Appointments" ADD "FollowUpAppointmentId" uuid;

ALTER TABLE "Appointments" ADD "ReservationId" uuid;

ALTER TABLE "Appointments" ADD "Revision" bigint NOT NULL DEFAULT 1;

CREATE INDEX "IX_Appointments_TenantId_FacilityId" ON "Appointments" ("TenantId", "FacilityId");

CREATE INDEX "IX_Appointments_TenantId_FollowUpAppointmentId" ON "Appointments" ("TenantId", "FollowUpAppointmentId");

CREATE UNIQUE INDEX "IX_Appointments_TenantId_ReservationId" ON "Appointments" ("TenantId", "ReservationId") WHERE "ReservationId" IS NOT NULL;

ALTER TABLE "Appointments" ADD CONSTRAINT "FK_Appointments_AppointmentReservations_TenantId_ReservationId" FOREIGN KEY ("TenantId", "ReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Appointments" ADD CONSTRAINT "FK_Appointments_Appointments_TenantId_FollowUpAppointmentId" FOREIGN KEY ("TenantId", "FollowUpAppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Appointments" ADD CONSTRAINT "FK_Appointments_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Appointments" ADD CONSTRAINT "FK_Appointments_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Appointments" ADD CONSTRAINT booking_source_shape CHECK (
 "Revision">0 AND (("ReservationId" IS NULL AND "FacilityId" IS NULL AND "BookingKind" IS NULL AND "FollowUpAppointmentId" IS NULL AND "BookingPolicyVersion" IS NULL)
 OR COALESCE("ReservationId" IS NOT NULL AND "FacilityId" IS NOT NULL AND "DoctorUserId" IS NOT NULL
  AND "BookingKind" IN ('routine','walk_in','follow_up') AND ("BookingKind"='follow_up')=("FollowUpAppointmentId" IS NOT NULL)
  AND length("BookingPolicyVersion") BETWEEN 1 AND 100 AND "DurationMinutes" BETWEEN 1 AND 480,FALSE)));
CREATE FUNCTION public.guard_explicit_appointment_booking() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE held public."AppointmentReservations"%ROWTYPE;
BEGIN
 IF TG_OP='DELETE' THEN
  IF OLD."ReservationId" IS NOT NULL THEN RAISE EXCEPTION 'Booked source history is retained' USING ERRCODE='23514'; END IF;
  RETURN OLD;
 END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF TG_OP='UPDATE' THEN
  IF OLD."ReservationId" IS NULL THEN
   IF NEW."ReservationId" IS NOT NULL OR NEW."FacilityId" IS NOT NULL OR NEW."BookingKind" IS NOT NULL THEN
    RAISE EXCEPTION 'Historical appointments require reviewed venue migration, never implicit binding' USING ERRCODE='23514';
   END IF;
   IF NEW."Status" IN (0,1) AND NOT NEW."IsDeleted"
    AND (NEW."DoctorUserId",NEW."ScheduledAt",NEW."DurationMinutes",NEW."Status",NEW."IsDeleted")
       IS DISTINCT FROM (OLD."DoctorUserId",OLD."ScheduledAt",OLD."DurationMinutes",OLD."Status",OLD."IsDeleted")
    AND (NEW."DurationMinutes" NOT BETWEEN 1 AND 480 OR EXISTS(
      SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId"
       AND s."TargetType"='practitioner' AND s."TargetId"=NEW."DoctorUserId" AND s."State" IN ('held','confirmed')
       AND s."StartsAtUtc"<NEW."ScheduledAt"+make_interval(mins=>LEAST(GREATEST(NEW."DurationMinutes",1),480))
       AND s."EndsAtUtc">NEW."ScheduledAt")) THEN
    RAISE EXCEPTION 'Legacy practitioner move conflicts with retained reservations or requires duration review' USING ERRCODE='23P01';
   END IF;
   RETURN NEW;
  END IF;
  IF (to_jsonb(OLD)-ARRAY['UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['UpdatedAt']) THEN
   RAISE EXCEPTION 'Reserved source changes require reviewed revisioned transition' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
 END IF;
 IF NEW."ReservationId" IS NULL OR NEW."Revision"<>1 OR NEW."Status"<>0 OR NEW."CheckedInAt" IS NOT NULL OR NEW."CompletedAt" IS NOT NULL
  OR NOT NEW."IsActive" OR NEW."IsDeleted" THEN RAISE EXCEPTION 'New booking requires explicit held source and initial state' USING ERRCODE='23514'; END IF;
 SELECT * INTO held FROM public."AppointmentReservations" h WHERE h."TenantId"=NEW."TenantId" AND h."Id"=NEW."ReservationId" FOR UPDATE;
 IF NOT FOUND OR held."State"<>'held' OR held."HoldExpiresAtUtc"<=clock_timestamp()
  OR held."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
  OR NEW."CreatedBy" IS DISTINCT FROM held."OwnerUserId" OR held."FacilityId"<>NEW."FacilityId" OR held."PractitionerUserId"<>NEW."DoctorUserId"
  OR held."StartsAtUtc"<>NEW."ScheduledAt" OR NEW."DurationMinutes"<>EXTRACT(EPOCH FROM(held."EndsAtUtc"-held."StartsAtUtc"))/60 THEN
  RAISE EXCEPTION 'New appointment must match exact live owned hold' USING ERRCODE='23514';
 END IF;
 IF public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
  OR NOT EXISTS(SELECT 1 FROM public."Patients" p WHERE p."TenantId"=NEW."TenantId" AND p."Id"=NEW."PatientId" AND p."IsActive" AND NOT p."IsDeleted")
  OR NOT EXISTS(SELECT 1 FROM public."PatientIdentifiers" i WHERE i."TenantId"=NEW."TenantId" AND i."FacilityId"=NEW."FacilityId"
    AND i."PatientId"=NEW."PatientId" AND i."Namespace"='local-mrn') THEN
  RAISE EXCEPTION 'Booking requires current scoped patient/facility identifier' USING ERRCODE='23514';
 END IF;
 IF NEW."BookingKind"='follow_up' AND NOT EXISTS(SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId"
  AND a."Id"=NEW."FollowUpAppointmentId" AND a."PatientId"=NEW."PatientId" AND a."ScheduledAt"<NEW."ScheduledAt"
  AND a."Status" NOT IN (3,4) AND a."IsActive" AND NOT a."IsDeleted") THEN
  RAISE EXCEPTION 'Follow-up must preserve exact same patient source' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER explicit_appointment_booking_guard BEFORE INSERT OR UPDATE OR DELETE ON "Appointments"
 FOR EACH ROW EXECUTE FUNCTION public.guard_explicit_appointment_booking();
CREATE FUNCTION public.require_appointment_reservation_binding() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE source_id uuid; scope_tenant uuid;
BEGIN
 IF TG_TABLE_NAME='Appointments' THEN source_id=NEW."Id"; scope_tenant=NEW."TenantId";
 ELSE source_id=NEW."AppointmentId"; scope_tenant=NEW."TenantId"; END IF;
 IF source_id IS NOT NULL AND EXISTS(SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=scope_tenant AND a."Id"=source_id AND a."ReservationId" IS NOT NULL)
  AND NOT EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentReservations" h
   ON h."TenantId"=a."TenantId" AND h."Id"=a."ReservationId"
   WHERE a."TenantId"=scope_tenant AND a."Id"=source_id AND h."AppointmentId"=a."Id" AND h."State"='confirmed'
    AND a."FacilityId"=h."FacilityId" AND a."DoctorUserId"=h."PractitionerUserId" AND a."ScheduledAt"=h."StartsAtUtc"
    AND a."DurationMinutes"=EXTRACT(EPOCH FROM(h."EndsAtUtc"-h."StartsAtUtc"))/60) THEN
  RAISE EXCEPTION 'Appointment and exact confirmed hold must commit atomically' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER appointment_reservation_binding_commit AFTER INSERT OR UPDATE ON "Appointments"
 DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_reservation_binding();
CREATE CONSTRAINT TRIGGER reservation_appointment_binding_commit AFTER INSERT OR UPDATE ON "AppointmentReservations"
 DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_appointment_reservation_binding();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261008185741_AddExplicitAppointmentBookings', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "QueueCounters" (
    "Id" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ArrivalDate" date NOT NULL,
    "LastToken" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_QueueCounters" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_QueueCounters_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "QueueEntries" (
    "Id" uuid NOT NULL,
    "AppointmentId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ReservationId" uuid NOT NULL,
    "OperatingSettingId" uuid NOT NULL,
    "Timezone" character varying(100) NOT NULL,
    "ArrivalDate" date NOT NULL,
    "ArrivedAtUtc" timestamp with time zone NOT NULL,
    "Token" bigint NOT NULL,
    "AppointmentRevision" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_QueueEntries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_QueueEntries_AppointmentReservations_TenantId_ReservationId" FOREIGN KEY ("TenantId", "ReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueEntries_Appointments_TenantId_AppointmentId" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueEntries_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueEntries_FacilityOperatingSettings_OperatingSettingId" FOREIGN KEY ("OperatingSettingId") REFERENCES "FacilityOperatingSettings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueEntries_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_QueueCounters_TenantId_FacilityId_ArrivalDate" ON "QueueCounters" ("TenantId", "FacilityId", "ArrivalDate");

CREATE INDEX "IX_QueueEntries_OperatingSettingId" ON "QueueEntries" ("OperatingSettingId");

CREATE UNIQUE INDEX "IX_QueueEntries_TenantId_AppointmentId" ON "QueueEntries" ("TenantId", "AppointmentId");

CREATE UNIQUE INDEX "IX_QueueEntries_TenantId_FacilityId_ArrivalDate_Token" ON "QueueEntries" ("TenantId", "FacilityId", "ArrivalDate", "Token");

CREATE INDEX "IX_QueueEntries_TenantId_PatientId" ON "QueueEntries" ("TenantId", "PatientId");

CREATE INDEX "IX_QueueEntries_TenantId_ReservationId" ON "QueueEntries" ("TenantId", "ReservationId");

ALTER TABLE "QueueEntries" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "QueueEntries" FORCE ROW LEVEL SECURITY;
ALTER TABLE "QueueCounters" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "QueueCounters" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "QueueEntries" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_scope ON "QueueEntries" AS RESTRICTIVE FOR SELECT USING (public.cureflow_patient_read_allowed("TenantId","PatientId"));
CREATE POLICY tenant_scope ON "QueueCounters" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
ALTER TABLE "QueueEntries" ADD CONSTRAINT queue_arrival_shape CHECK ("Token" BETWEEN 1 AND 1000000000
 AND "AppointmentRevision">1 AND length("Timezone") BETWEEN 1 AND 100 AND "IsActive" AND NOT "IsDeleted"
 AND "CreatedBy" IS NOT NULL);
ALTER TABLE "QueueCounters" ADD CONSTRAINT queue_counter_shape CHECK ("LastToken" BETWEEN 1 AND 1000000000 AND "IsActive" AND NOT "IsDeleted");
DO $extend$ DECLARE definition text; original_condition text; replacement_condition text;
BEGIN
 definition=pg_get_functiondef('public.guard_explicit_appointment_booking()'::regprocedure);
 original_condition=$old$IF (to_jsonb(OLD)-ARRAY['UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['UpdatedAt']) THEN$old$;
 replacement_condition=$new$IF (to_jsonb(OLD)-ARRAY['UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['UpdatedAt'])
 AND NOT (OLD."CheckedInAt" IS NULL AND NEW."CheckedInAt" IS NOT NULL AND NEW."Revision"=OLD."Revision"+1
 AND NEW."Status" IN (0,1) AND (to_jsonb(OLD)-ARRAY['CheckedInAt','Revision','UpdatedAt','UpdatedBy'])
 IS NOT DISTINCT FROM (to_jsonb(NEW)-ARRAY['CheckedInAt','Revision','UpdatedAt','UpdatedBy'])) THEN$new$;
 IF position(original_condition IN definition)=0 THEN RAISE EXCEPTION 'Reviewed booking guard version is required'; END IF;
 EXECUTE replace(definition,original_condition,replacement_condition);
END $extend$;
CREATE FUNCTION public.guard_queue_arrival() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Arrival evidence is retained and immutable' USING ERRCODE='23514'; END IF;
 IF NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
 OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
 OR NOT EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentReservations" r
 ON r."TenantId"=a."TenantId" AND r."Id"=a."ReservationId"
 WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId" AND a."PatientId"=NEW."PatientId"
 AND a."FacilityId"=NEW."FacilityId" AND a."ReservationId"=NEW."ReservationId"
 AND a."Status" IN (0,1) AND a."CheckedInAt"=NEW."ArrivedAtUtc" AND a."Revision"=NEW."AppointmentRevision"
 AND a."IsActive" AND NOT a."IsDeleted" AND r."State"='confirmed' AND r."AppointmentId"=a."Id")
 OR NOT EXISTS(SELECT 1 FROM public."FacilityOperatingSettings" s WHERE s."TenantId"=NEW."TenantId"
 AND s."Id"=NEW."OperatingSettingId" AND s."FacilityId"=NEW."FacilityId" AND s."Timezone"=NEW."Timezone"
 AND NOT EXISTS(SELECT 1 FROM public."FacilityOperatingSettings" newer WHERE newer."TenantId"=s."TenantId"
 AND newer."FacilityId"=s."FacilityId" AND newer."Revision">s."Revision"))
 OR NEW."ArrivalDate"<>(NEW."ArrivedAtUtc" AT TIME ZONE NEW."Timezone")::date THEN
 RAISE EXCEPTION 'Arrival requires exact current facility appointment and timezone evidence' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER queue_arrival_guard BEFORE INSERT OR UPDATE OR DELETE ON "QueueEntries" FOR EACH ROW EXECUTE FUNCTION public.guard_queue_arrival();
CREATE FUNCTION public.guard_queue_counter() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Queue token allocation is retained' USING ERRCODE='23514'; END IF;
 IF public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
 OR (TG_OP='INSERT' AND NEW."LastToken"<>1)
 OR (TG_OP='UPDATE' AND (NEW."LastToken"<>OLD."LastToken"+1
 OR (to_jsonb(OLD)-ARRAY['LastToken','UpdatedAt','UpdatedBy']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['LastToken','UpdatedAt','UpdatedBy']))) THEN
 RAISE EXCEPTION 'Queue tokens allocate once in retained facility/date scope' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER queue_counter_guard BEFORE INSERT OR UPDATE OR DELETE ON "QueueCounters" FOR EACH ROW EXECUTE FUNCTION public.guard_queue_counter();
CREATE FUNCTION public.require_queue_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE last_token bigint;
BEGIN
 SELECT c."LastToken" INTO last_token FROM public."QueueCounters" c WHERE c."TenantId"=NEW."TenantId"
 AND c."FacilityId"=NEW."FacilityId" AND c."ArrivalDate"=NEW."ArrivalDate";
 IF last_token IS NULL OR NOT EXISTS(SELECT 1 FROM public."QueueEntries" q WHERE q."TenantId"=NEW."TenantId"
 AND q."FacilityId"=NEW."FacilityId" AND q."ArrivalDate"=NEW."ArrivalDate" AND q."Token"=last_token) THEN
 RAISE EXCEPTION 'Token and exact arrival must commit together' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER queue_counter_commit AFTER INSERT OR UPDATE ON "QueueCounters" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_queue_commit();
CREATE CONSTRAINT TRIGGER queue_entry_commit AFTER INSERT ON "QueueEntries" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_queue_commit();
CREATE FUNCTION public.retain_appointment_arrival() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF OLD."CheckedInAt" IS NOT NULL AND (NEW."CheckedInAt" IS DISTINCT FROM OLD."CheckedInAt"
 OR NEW."PatientId"<>OLD."PatientId" OR NEW."FacilityId" IS DISTINCT FROM OLD."FacilityId"
 OR NEW."ReservationId" IS DISTINCT FROM OLD."ReservationId" OR NEW."ScheduledAt"<>OLD."ScheduledAt"
 OR NEW."DurationMinutes"<>OLD."DurationMinutes" OR NEW."Status" IN (3,4) OR NEW."IsDeleted") THEN
 RAISE EXCEPTION 'Checked-in source requires a reviewed queue transition' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_arrival_retained BEFORE UPDATE ON "Appointments" FOR EACH ROW EXECUTE FUNCTION public.retain_appointment_arrival();
CREATE FUNCTION public.require_appointment_arrival_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF OLD."CheckedInAt" IS NULL AND NEW."CheckedInAt" IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM public."QueueEntries" q WHERE q."TenantId"=NEW."TenantId" AND q."AppointmentId"=NEW."Id"
 AND q."PatientId"=NEW."PatientId" AND q."FacilityId"=NEW."FacilityId" AND q."ReservationId"=NEW."ReservationId"
 AND q."ArrivedAtUtc"=NEW."CheckedInAt" AND q."AppointmentRevision"=NEW."Revision") THEN
 RAISE EXCEPTION 'New check-in and exact queue evidence must commit together' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER appointment_arrival_commit AFTER UPDATE ON "Appointments" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_appointment_arrival_commit();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "QueueEntries","QueueCounters" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "QueueEntries","QueueCounters" FROM cureflow_tenant;
 GRANT UPDATE ("LastToken","UpdatedAt","UpdatedBy") ON "QueueCounters" TO cureflow_tenant;
END IF; END $grant$;
CREATE OR REPLACE FUNCTION public.enforce_reschedule_work() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE exception_row public."ScheduleExceptions"; appointment_row public."Appointments";
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Original booking impact is immutable' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF NEW."IsDeleted" OR NOT NEW."IsActive" OR NEW."State"<>'needs_review' THEN
 RAISE EXCEPTION 'Invalid initial booking impact evidence' USING ERRCODE='23514'; END IF;
 SELECT * INTO exception_row FROM public."ScheduleExceptions" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."ExceptionId" AND "IsActive";
 IF NOT FOUND THEN RAISE EXCEPTION 'Current same-tenant exception required' USING ERRCODE='23514'; END IF;
 SELECT * INTO appointment_row FROM public."Appointments" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."AppointmentId"
 AND "IsActive" AND NOT "IsDeleted" AND "Status" IN (0,1) FOR SHARE;
 IF NOT FOUND OR appointment_row."PatientId"<>NEW."PatientId" OR appointment_row."ScheduledAt"<>NEW."OriginalScheduledAtUtc"
 OR appointment_row."DurationMinutes"<>NEW."OriginalDurationMinutes" OR NEW."OriginalDurationMinutes" NOT BETWEEN 1 AND 1440
 OR (exception_row."PractitionerUserId" IS NOT NULL AND appointment_row."DoctorUserId" IS DISTINCT FROM exception_row."PractitionerUserId")
 OR NEW."FacilityAssociationNeedsReview" IS DISTINCT FROM (appointment_row."FacilityId" IS NULL)
 OR (appointment_row."FacilityId" IS NOT NULL AND appointment_row."FacilityId"<>exception_row."FacilityId")
 OR (appointment_row."FacilityId" IS NULL AND exception_row."ResourceId" IS NOT NULL AND exception_row."PractitionerUserId" IS NULL)
 OR (appointment_row."FacilityId" IS NOT NULL AND exception_row."ResourceId" IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId"
 AND s."ReservationId"=appointment_row."ReservationId" AND s."TargetType"='resource' AND s."TargetId"=exception_row."ResourceId" AND s."State"='confirmed'))
 OR appointment_row."ScheduledAt">=exception_row."EndsAtUtc"
 OR appointment_row."ScheduledAt"+make_interval(mins=>appointment_row."DurationMinutes")<=exception_row."StartsAtUtc" THEN
 RAISE EXCEPTION 'Impact must retain the exact overlapping source booking' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $body$;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009100817_AddFacilityQueueArrivals', '10.0.0');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_AppointmentReservations_TenantId_AppointmentId";

CREATE TABLE "AppointmentBookingChanges" (
    "Id" uuid NOT NULL,
    "AppointmentId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "AppointmentRevision" bigint NOT NULL,
    "Action" character varying(20) NOT NULL,
    "PreviousReservationId" uuid NOT NULL,
    "ReplacementReservationId" uuid,
    "PreviousStartsAtUtc" timestamp with time zone NOT NULL,
    "PreviousEndsAtUtc" timestamp with time zone NOT NULL,
    "ReplacementStartsAtUtc" timestamp with time zone,
    "ReplacementEndsAtUtc" timestamp with time zone,
    "Reason" character varying(1000) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentBookingChanges" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppointmentBookingChanges_AppointmentReservations_TenantId_~" FOREIGN KEY ("TenantId", "PreviousReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentBookingChanges_AppointmentReservations_TenantId~1" FOREIGN KEY ("TenantId", "ReplacementReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentBookingChanges_Appointments_TenantId_Appointment~" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentBookingChanges_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_AppointmentReservations_TenantId_AppointmentId" ON "AppointmentReservations" ("TenantId", "AppointmentId") WHERE "AppointmentId" IS NOT NULL AND "State" = 'confirmed';

CREATE UNIQUE INDEX "IX_AppointmentBookingChanges_TenantId_AppointmentId_Appointmen~" ON "AppointmentBookingChanges" ("TenantId", "AppointmentId", "AppointmentRevision");

CREATE INDEX "IX_AppointmentBookingChanges_TenantId_PatientId" ON "AppointmentBookingChanges" ("TenantId", "PatientId");

CREATE INDEX "IX_AppointmentBookingChanges_TenantId_PreviousReservationId" ON "AppointmentBookingChanges" ("TenantId", "PreviousReservationId");

CREATE INDEX "IX_AppointmentBookingChanges_TenantId_ReplacementReservationId" ON "AppointmentBookingChanges" ("TenantId", "ReplacementReservationId");

ALTER TABLE "AppointmentBookingChanges" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "AppointmentBookingChanges" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope ON "AppointmentBookingChanges" USING ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid)
 WITH CHECK ("TenantId"=NULLIF(current_setting('app.current_tenant_id',true),'')::uuid);
CREATE POLICY patient_confidentiality ON "AppointmentBookingChanges" AS RESTRICTIVE FOR SELECT
 USING(public.cureflow_patient_read_allowed("TenantId","PatientId"));
ALTER TABLE "AppointmentBookingChanges" ADD CONSTRAINT booking_change_shape CHECK(COALESCE(
 "AppointmentRevision">1 AND length("Reason") BETWEEN 3 AND 1000 AND length("PolicyVersion") BETWEEN 1 AND 100
 AND "IsActive" AND NOT "IsDeleted" AND "PreviousStartsAtUtc"<"PreviousEndsAtUtc"
 AND (("Action"='cancel' AND "ReplacementReservationId" IS NULL AND "ReplacementStartsAtUtc" IS NULL AND "ReplacementEndsAtUtc" IS NULL)
 OR ("Action"='reschedule' AND "ReplacementReservationId" IS NOT NULL AND "ReplacementReservationId"<>"PreviousReservationId"
 AND "ReplacementStartsAtUtc"<"ReplacementEndsAtUtc")),false));
CREATE FUNCTION public.guard_appointment_booking_change() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE source public."Appointments"%ROWTYPE; previous public."AppointmentReservations"%ROWTYPE; next_hold public."AppointmentReservations"%ROWTYPE;
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Booking change evidence is immutable' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 SELECT * INTO source FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId" FOR UPDATE;
 IF NOT FOUND OR source."PatientId"<>NEW."PatientId" OR source."Revision"+1<>NEW."AppointmentRevision"
  OR source."ReservationId"<>NEW."PreviousReservationId" OR source."ScheduledAt"<>NEW."PreviousStartsAtUtc"
  OR source."ScheduledAt"+make_interval(mins=>source."DurationMinutes")<>NEW."PreviousEndsAtUtc"
  OR source."CheckedInAt" IS NOT NULL OR source."CompletedAt" IS NOT NULL OR source."Status" NOT IN(0,1)
  OR source."ScheduledAt"<=clock_timestamp() OR source."IsDeleted"
  OR NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid THEN
  RAISE EXCEPTION 'Change must record exact future unchecked-in patient booking source' USING ERRCODE='23514';
 END IF;
 SELECT * INTO previous FROM public."AppointmentReservations" h WHERE h."TenantId"=NEW."TenantId" AND h."Id"=NEW."PreviousReservationId" FOR UPDATE;
 IF NOT FOUND OR previous."State"<>'confirmed' OR previous."AppointmentId"<>source."Id" THEN
  RAISE EXCEPTION 'Change requires exact prior confirmed allocation' USING ERRCODE='23514';
 END IF;
 IF NEW."Action"='reschedule' THEN
  SELECT * INTO next_hold FROM public."AppointmentReservations" h WHERE h."TenantId"=NEW."TenantId" AND h."Id"=NEW."ReplacementReservationId" FOR UPDATE;
  IF NOT FOUND OR next_hold."State"<>'held' OR next_hold."HoldExpiresAtUtc"<=clock_timestamp() OR next_hold."StartsAtUtc"<=clock_timestamp()
   OR next_hold."OwnerUserId" IS DISTINCT FROM NEW."CreatedBy" OR NEW."ReplacementStartsAtUtc"<>next_hold."StartsAtUtc"
   OR NEW."ReplacementEndsAtUtc"<>next_hold."EndsAtUtc" THEN
   RAISE EXCEPTION 'Replacement evidence must match current exact owned hold' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_booking_change_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentBookingChanges"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_booking_change();
CREATE FUNCTION public.require_booking_change_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentReservations" previous
   ON previous."TenantId"=a."TenantId" AND previous."Id"=NEW."PreviousReservationId"
  WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId" AND a."PatientId"=NEW."PatientId" AND a."Revision"=NEW."AppointmentRevision"
   AND previous."State"='released' AND previous."AppointmentId"=a."Id" AND a."CheckedInAt" IS NULL
   AND ((NEW."Action"='cancel' AND a."Status"=4 AND a."ReservationId"=previous."Id")
    OR (NEW."Action"='reschedule' AND a."Status" IN(0,1) AND a."ReservationId"=NEW."ReplacementReservationId"
     AND a."ScheduledAt"=NEW."ReplacementStartsAtUtc" AND a."ScheduledAt"+make_interval(mins=>a."DurationMinutes")=NEW."ReplacementEndsAtUtc"))) THEN
  RAISE EXCEPTION 'Booking, prior release and replacement/cancellation must commit atomically' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER booking_change_commit AFTER INSERT ON "AppointmentBookingChanges" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_booking_change_commit();
ALTER TABLE "AppointmentReservations" DROP CONSTRAINT reservation_shape;
ALTER TABLE "AppointmentReservations" ADD CONSTRAINT reservation_shape CHECK(COALESCE(
 "StartsAtUtc"<"EndsAtUtc" AND "EndsAtUtc"<="StartsAtUtc"+interval '8 hours'
 AND "HoldExpiresAtUtc">"CreatedAt" AND "HoldExpiresAtUtc"<="CreatedAt"+interval '30 minutes' AND "HoldExpiresAtUtc"<="EndsAtUtc"
 AND length("PolicyVersion") BETWEEN 1 AND 100 AND "CreatedBy"="OwnerUserId" AND "IsActive" AND NOT "IsDeleted"
 AND (("State"='held' AND "Revision"=1 AND "AppointmentId" IS NULL)
 OR ("State" IN('released','expired') AND "Revision"=2 AND "AppointmentId" IS NULL)
 OR ("State"='confirmed' AND "Revision"=2 AND "AppointmentId" IS NOT NULL)
 OR ("State"='released' AND "Revision"=3 AND "AppointmentId" IS NOT NULL)),false));
CREATE OR REPLACE FUNCTION public.require_appointment_reservation_binding() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE source_id uuid; scope_tenant uuid;
BEGIN
 IF TG_TABLE_NAME='Appointments' THEN source_id=NEW."Id"; scope_tenant=NEW."TenantId";
 ELSE source_id=NEW."AppointmentId"; scope_tenant=NEW."TenantId"; END IF;
 IF source_id IS NOT NULL AND EXISTS(SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=scope_tenant AND a."Id"=source_id AND a."ReservationId" IS NOT NULL)
  AND NOT EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentReservations" h ON h."TenantId"=a."TenantId" AND h."Id"=a."ReservationId"
   WHERE a."TenantId"=scope_tenant AND a."Id"=source_id AND h."AppointmentId"=a."Id"
    AND ((a."Status" IN(0,1) AND h."State"='confirmed') OR(a."Status"=4 AND h."State"='released'))
    AND a."FacilityId"=h."FacilityId" AND a."DoctorUserId"=h."PractitionerUserId" AND a."ScheduledAt"=h."StartsAtUtc"
    AND a."DurationMinutes"=EXTRACT(EPOCH FROM(h."EndsAtUtc"-h."StartsAtUtc"))/60) THEN
  RAISE EXCEPTION 'Appointment and exact current confirmed/released allocation must commit atomically' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "AppointmentBookingChanges" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "AppointmentBookingChanges" FROM cureflow_tenant;
END IF; END $grant$;
CREATE OR REPLACE FUNCTION public.guard_appointment_reservation() RETURNS trigger LANGUAGE plpgsql AS $guard$
                BEGIN
                 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Reservation history is retained' USING ERRCODE='23514'; END IF;
                 IF TG_OP='INSERT' THEN
                  IF NEW."State"<>'held' OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
                   OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
                   OR NOT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."OwnerUserId" AND u."IsActive" AND NOT u."IsDeleted") THEN
                   RAISE EXCEPTION 'Hold requires current owner and active facility' USING ERRCODE='23514';
                  END IF;
                 ELSE
                  IF NOT((OLD."State"='held' AND NEW."State" IN('confirmed','released','expired'))
 OR(OLD."State"='confirmed' AND NEW."State"='released' AND NEW."AppointmentId"=OLD."AppointmentId"
 AND EXISTS(SELECT 1 FROM public."Appointments" a JOIN public."AppointmentBookingChanges" c ON c."TenantId"=a."TenantId" AND c."AppointmentId"=a."Id"
  WHERE a."TenantId"=NEW."TenantId" AND a."Id"=OLD."AppointmentId" AND a."ReservationId"=OLD."Id"
   AND a."ScheduledAt">clock_timestamp() AND a."CheckedInAt" IS NULL AND a."CompletedAt" IS NULL AND a."Status" IN(0,1)
   AND c."PreviousReservationId"=OLD."Id" AND c."AppointmentRevision"=a."Revision"+1
   AND c."CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid))) OR NEW."Revision"<>OLD."Revision"+1
                   OR (to_jsonb(OLD)-ARRAY['State','Revision','AppointmentId','UpdatedAt'])
                      IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) THEN
                   RAISE EXCEPTION 'Reservation source is immutable; transition is terminal' USING ERRCODE='23514';
                  END IF;
                  IF NEW."State"='expired' THEN
                   IF OLD."HoldExpiresAtUtc">clock_timestamp() THEN RAISE EXCEPTION 'Live hold cannot expire' USING ERRCODE='23514'; END IF;
                  ELSIF OLD."State"='held' AND NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid THEN
                   RAISE EXCEPTION 'Reservation owner is required' USING ERRCODE='23514';
                  END IF;
                  IF NEW."State"='confirmed' AND (OLD."HoldExpiresAtUtc"<=clock_timestamp() OR NOT EXISTS(
                   SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId"
                    AND (a."CreatedAt">=OLD."CreatedAt" OR EXISTS(SELECT 1 FROM public."AppointmentBookingChanges" c
 WHERE c."TenantId"=a."TenantId" AND c."AppointmentId"=a."Id" AND c."AppointmentRevision"=a."Revision"
 AND c."Action"='reschedule' AND c."ReplacementReservationId"=NEW."Id")) AND a."FacilityId"=NEW."FacilityId" AND a."ReservationId"=NEW."Id" AND a."DoctorUserId"=NEW."PractitionerUserId" AND a."ScheduledAt"=NEW."StartsAtUtc"
                    AND a."DurationMinutes"=EXTRACT(EPOCH FROM (NEW."EndsAtUtc"-NEW."StartsAtUtc"))/60 AND a."Status" IN (0,1) AND NOT a."IsDeleted")) THEN
                   RAISE EXCEPTION 'Expired hold or mismatched new appointment cannot confirm' USING ERRCODE='23514';
                  END IF;
                 END IF;
                 RETURN NEW;
                END $guard$;
CREATE OR REPLACE FUNCTION public.guard_appointment_reservation_seat() RETURNS trigger LANGUAGE plpgsql AS $guard$
                DECLARE reservation public."AppointmentReservations"%ROWTYPE; capacity integer;
                BEGIN
                 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Reservation occupancy history is retained' USING ERRCODE='23514'; END IF;
                 SELECT * INTO reservation FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReservationId";
                 IF NOT FOUND OR NEW."StartsAtUtc"<>reservation."StartsAtUtc" OR NEW."EndsAtUtc"<>reservation."EndsAtUtc" OR NEW."State"<>reservation."State" THEN
                  RAISE EXCEPTION 'Occupancy must match exact source reservation' USING ERRCODE='23514';
                 END IF;
                 IF TG_OP='INSERT' THEN
                  IF NEW."State"<>'held' THEN RAISE EXCEPTION 'Occupancy starts held' USING ERRCODE='23514'; END IF;
                  PERFORM pg_advisory_xact_lock(734291);
                  PERFORM pg_advisory_xact_lock(hashtextextended('appointment-seat:'||NEW."TenantId"::text||':'||NEW."TargetType"||':'||NEW."TargetId"::text,0));
                  IF NEW."TargetType"='practitioner' THEN
                   capacity=1;
                   IF NEW."TargetId"<>reservation."PractitionerUserId" OR NOT EXISTS(
                    SELECT 1 FROM public."Users" u JOIN public."StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
                     WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."TargetId" AND u."IsActive" AND NOT u."IsDeleted"
                      AND p."IsActive" AND NOT p."IsDeleted" AND p."IsAvailable") THEN RAISE EXCEPTION 'Practitioner scope unavailable' USING ERRCODE='23514'; END IF;
                  ELSE
                   SELECT r."Capacity" INTO capacity FROM public."FacilityResources" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."TargetId"
                    AND r."FacilityId"=reservation."FacilityId" AND r."ResourceType" IN ('room','equipment') AND r."IsActive" AND NOT r."IsDeleted"
                    AND r."OperationalStatus"='enabled' AND r."EffectiveFrom"<=NEW."StartsAtUtc" AND (r."EffectiveUntil" IS NULL OR r."EffectiveUntil">=NEW."EndsAtUtc") FOR SHARE;
                  END IF;
                  IF capacity IS NULL OR NEW."CapacitySnapshot"<>capacity OR NEW."Seat">capacity THEN
                   RAISE EXCEPTION 'Seat is outside current resource capacity' USING ERRCODE='23514';
                  END IF;
                 ELSE
                  IF NOT((OLD."State"='held' AND NEW."State" IN('confirmed','released','expired')) OR(OLD."State"='confirmed' AND NEW."State"='released'))
                   OR (to_jsonb(OLD)-ARRAY['State','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','UpdatedAt']) THEN
                   RAISE EXCEPTION 'Occupancy identity/range cannot change; transition is terminal' USING ERRCODE='23514';
                  END IF;
                 END IF;
                 RETURN NEW;
                END $guard$;
DO $extend$ DECLARE definition text; insertion_point text;
BEGIN
 definition=pg_get_functiondef('public.guard_explicit_appointment_booking()'::regprocedure);
 insertion_point=$needle$ IF TG_OP='UPDATE' THEN$needle$;
 IF position(insertion_point IN definition)=0 OR position('AppointmentBookingChanges' IN definition)>0 THEN
  RAISE EXCEPTION 'Reviewed booking/arrival guard version is required';
 END IF;
 EXECUTE replace(definition,insertion_point,$branch$
 IF TG_OP='UPDATE' AND (NEW."PatientId",NEW."ScheduledAt",NEW."Status")
   IS DISTINCT FROM (OLD."PatientId",OLD."ScheduledAt",OLD."Status")
  AND EXISTS(SELECT 1 FROM public."Appointments" child WHERE child."TenantId"=OLD."TenantId" AND child."FollowUpAppointmentId"=OLD."Id"
   AND child."Status" IN(0,1) AND NOT child."IsDeleted" AND child."IsActive"
   AND (NEW."PatientId"<>child."PatientId" OR NEW."Status" IN(3,4) OR NEW."ScheduledAt">=child."ScheduledAt")) THEN
  RAISE EXCEPTION 'Active follow-up references require reviewed reconciliation before prior source changes' USING ERRCODE='23514';
 END IF;
 IF TG_OP='UPDATE' AND OLD."ReservationId" IS NOT NULL AND EXISTS(
   SELECT 1 FROM public."AppointmentBookingChanges" c WHERE c."TenantId"=NEW."TenantId" AND c."AppointmentId"=NEW."Id"
    AND c."AppointmentRevision"=NEW."Revision") THEN
  IF OLD."CheckedInAt" IS NOT NULL OR OLD."CompletedAt" IS NOT NULL OR OLD."Status" NOT IN(0,1) OR OLD."ScheduledAt"<=clock_timestamp()
   OR NEW."Revision"<>OLD."Revision"+1
   OR (to_jsonb(OLD)-ARRAY['FacilityId','ReservationId','DoctorUserId','DoctorName','Department','ScheduledAt','DurationMinutes','Status','Revision','UpdatedAt'])
      IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['FacilityId','ReservationId','DoctorUserId','DoctorName','Department','ScheduledAt','DurationMinutes','Status','Revision','UpdatedAt'])
   OR NOT EXISTS(SELECT 1 FROM public."AppointmentBookingChanges" c WHERE c."TenantId"=NEW."TenantId" AND c."AppointmentId"=NEW."Id"
    AND c."PatientId"=OLD."PatientId" AND c."PreviousReservationId"=OLD."ReservationId" AND c."AppointmentRevision"=NEW."Revision"
    AND c."CreatedBy"=NULLIF(current_setting('app.current_user_id',true),'')::uuid
    AND ((c."Action"='cancel' AND NEW."Status"=4
      AND (to_jsonb(OLD)-ARRAY['Status','Revision','UpdatedAt']) IS NOT DISTINCT FROM (to_jsonb(NEW)-ARRAY['Status','Revision','UpdatedAt']))
     OR(c."Action"='reschedule' AND NEW."Status"=OLD."Status" AND c."ReplacementReservationId"=NEW."ReservationId"
      AND EXISTS(SELECT 1 FROM public."AppointmentReservations" h WHERE h."TenantId"=NEW."TenantId" AND h."Id"=NEW."ReservationId"
       AND h."State"='held' AND h."HoldExpiresAtUtc">clock_timestamp() AND h."FacilityId"=NEW."FacilityId"
       AND h."PractitionerUserId"=NEW."DoctorUserId" AND h."StartsAtUtc"=NEW."ScheduledAt"
       AND NEW."DurationMinutes"=EXTRACT(EPOCH FROM(h."EndsAtUtc"-h."StartsAtUtc"))/60)))) THEN
   RAISE EXCEPTION 'Reserved transition requires exact immutable change evidence' USING ERRCODE='23514';
  END IF;
  IF NEW."Status"<>4 AND NEW."FollowUpAppointmentId" IS NOT NULL AND NOT EXISTS(
    SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."FollowUpAppointmentId"
     AND a."PatientId"=NEW."PatientId" AND a."ScheduledAt"<NEW."ScheduledAt" AND a."Status" NOT IN(3,4) AND NOT a."IsDeleted") THEN
   RAISE EXCEPTION 'Rescheduled follow-up must retain valid earlier same-patient source' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
 END IF;
$branch$||insertion_point);
END $extend$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009101224_AddRetainedAppointmentChanges', '10.0.0');

COMMIT;

START TRANSACTION;
ALTER TABLE "QueueEntries" ADD CONSTRAINT "AK_QueueEntries_TenantId_Id" UNIQUE ("TenantId", "Id");

CREATE TABLE "QueuePriorityOverrides" (
    "Id" uuid NOT NULL,
    "QueueEntryId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ArrivalDate" date NOT NULL,
    "Revision" bigint NOT NULL,
    "SortRank" integer NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_QueuePriorityOverrides" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_QueuePriorityOverrides_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueuePriorityOverrides_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueuePriorityOverrides_QueueEntries_TenantId_QueueEntryId" FOREIGN KEY ("TenantId", "QueueEntryId") REFERENCES "QueueEntries" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "QueueStarts" (
    "Id" uuid NOT NULL,
    "QueueEntryId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "ArrivalDate" date NOT NULL,
    "Revision" bigint NOT NULL,
    "SortRank" integer NOT NULL,
    "DefaultRank" integer NOT NULL,
    "PolicyVersion" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_QueueStarts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_QueueStarts_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueStarts_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QueueStarts_QueueEntries_TenantId_QueueEntryId" FOREIGN KEY ("TenantId", "QueueEntryId") REFERENCES "QueueEntries" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_QueuePriorityOverrides_TenantId_FacilityId_ArrivalDate" ON "QueuePriorityOverrides" ("TenantId", "FacilityId", "ArrivalDate");

CREATE INDEX "IX_QueuePriorityOverrides_TenantId_PatientId" ON "QueuePriorityOverrides" ("TenantId", "PatientId");

CREATE UNIQUE INDEX "IX_QueuePriorityOverrides_TenantId_QueueEntryId_Revision" ON "QueuePriorityOverrides" ("TenantId", "QueueEntryId", "Revision");

CREATE INDEX "IX_QueueStarts_TenantId_FacilityId_ArrivalDate" ON "QueueStarts" ("TenantId", "FacilityId", "ArrivalDate");

CREATE INDEX "IX_QueueStarts_TenantId_PatientId" ON "QueueStarts" ("TenantId", "PatientId");

CREATE UNIQUE INDEX "IX_QueueStarts_TenantId_QueueEntryId" ON "QueueStarts" ("TenantId", "QueueEntryId");

DO $scope$ DECLARE tab text; BEGIN
 FOREACH tab IN ARRAY ARRAY['QueuePriorityOverrides','QueueStarts'] LOOP
 EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',tab);
 EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',tab);
 EXECUTE format('CREATE POLICY tenant_scope ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',tab);
 EXECUTE format('CREATE POLICY patient_scope ON %I AS RESTRICTIVE FOR SELECT USING (public.cureflow_patient_read_allowed("TenantId","PatientId"))',tab);
 EXECUTE format('ALTER TABLE %I ADD CONSTRAINT dispatch_shape CHECK ("Revision">1 AND "SortRank" BETWEEN 0 AND 1000 AND length("PolicyVersion") BETWEEN 1 AND 100 AND "CreatedBy" IS NOT NULL AND "IsActive" AND NOT "IsDeleted")',tab);
 END LOOP;
END $scope$;
ALTER TABLE "QueuePriorityOverrides" ADD CONSTRAINT queue_override_reason CHECK (length(btrim("Reason")) BETWEEN 3 AND 500);
ALTER TABLE "QueueStarts" ADD CONSTRAINT queue_default_rank CHECK ("DefaultRank" BETWEEN 0 AND 1000);
CREATE FUNCTION public.guard_queue_dispatch() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE entry public."QueueEntries"; prior_revision bigint; prior_rank integer; first_id uuid;
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Queue decisions are attributable retained evidence' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
 OR NOT EXISTS(SELECT 1 FROM public."Users" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."CreatedBy" AND "IsActive" AND NOT "IsDeleted")
 OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
 OR EXISTS(SELECT 1 FROM public."PatientConfidentialityStates" WHERE "TenantId"=NEW."TenantId" AND "Restricted"
 AND NOT public.cureflow_patient_read_allowed("TenantId","PatientId")) THEN
 RAISE EXCEPTION 'Current complete hospital queue authority is required' USING ERRCODE='42501'; END IF;
 SELECT * INTO entry FROM public."QueueEntries" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."QueueEntryId";
 IF NOT FOUND OR entry."FacilityId"<>NEW."FacilityId" OR entry."PatientId"<>NEW."PatientId" OR entry."ArrivalDate"<>NEW."ArrivalDate"
 OR NOT EXISTS(SELECT 1 FROM public."Appointments" WHERE "TenantId"=NEW."TenantId" AND "Id"=entry."AppointmentId"
 AND "PatientId"=entry."PatientId" AND "FacilityId"=entry."FacilityId" AND "CheckedInAt"=entry."ArrivedAtUtc"
 AND "Status" IN (0,1) AND "IsActive" AND NOT "IsDeleted") THEN
 RAISE EXCEPTION 'Current exact pending arrival is required' USING ERRCODE='23514'; END IF;
 IF EXISTS(SELECT 1 FROM public."QueueStarts" WHERE "TenantId"=NEW."TenantId" AND "QueueEntryId"=entry."Id") THEN
 RAISE EXCEPTION 'The queue item has already started' USING ERRCODE='23514'; END IF;
 SELECT "Revision","SortRank" INTO prior_revision,prior_rank FROM public."QueuePriorityOverrides"
 WHERE "TenantId"=NEW."TenantId" AND "QueueEntryId"=entry."Id" ORDER BY "Revision" DESC LIMIT 1;
 IF NEW."Revision"<>COALESCE(prior_revision,1)+1 THEN RAISE EXCEPTION 'The queue revision changed' USING ERRCODE='23514'; END IF;
 IF TG_TABLE_NAME='QueueStarts' THEN
 IF NEW."SortRank"<>COALESCE(prior_rank,NEW."DefaultRank") THEN RAISE EXCEPTION 'Start must retain current priority' USING ERRCODE='23514'; END IF;
 SELECT q."Id" INTO first_id FROM public."QueueEntries" q
 LEFT JOIN LATERAL(SELECT p."SortRank" FROM public."QueuePriorityOverrides" p WHERE p."TenantId"=q."TenantId" AND p."QueueEntryId"=q."Id" ORDER BY p."Revision" DESC LIMIT 1) p ON true
 WHERE q."TenantId"=NEW."TenantId" AND q."FacilityId"=NEW."FacilityId" AND q."ArrivalDate"=NEW."ArrivalDate"
 AND NOT EXISTS(SELECT 1 FROM public."QueueStarts" s WHERE s."TenantId"=q."TenantId" AND s."QueueEntryId"=q."Id")
 ORDER BY COALESCE(p."SortRank",NEW."DefaultRank"),q."Token" LIMIT 1;
 IF first_id IS DISTINCT FROM entry."Id" THEN RAISE EXCEPTION 'Only the current first queue item may start' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER queue_priority_guard BEFORE INSERT OR UPDATE OR DELETE ON "QueuePriorityOverrides" FOR EACH ROW EXECUTE FUNCTION public.guard_queue_dispatch();
CREATE TRIGGER queue_start_guard BEFORE INSERT OR UPDATE OR DELETE ON "QueueStarts" FOR EACH ROW EXECUTE FUNCTION public.guard_queue_dispatch();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "QueuePriorityOverrides","QueueStarts" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "QueuePriorityOverrides","QueueStarts" FROM cureflow_tenant;
END IF; END $grant$;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009115118_AddRecordedQueueDispatch', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "AppointmentWaitlistDecisions" (
    "Id" uuid NOT NULL,
    "EntryId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "EntryRevision" bigint NOT NULL,
    "OfferId" uuid,
    "Action" character varying(30) NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentWaitlistDecisions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppointmentWaitlistDecisions_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "AppointmentWaitlistEntries" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "FacilityId" uuid NOT NULL,
    "PractitionerUserId" uuid NOT NULL,
    "ResourceIdsJson" text NOT NULL,
    "EarliestStartsAtUtc" timestamp with time zone NOT NULL,
    "LatestEndsAtUtc" timestamp with time zone NOT NULL,
    "DurationMinutes" integer NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "BookingKind" character varying(20) NOT NULL,
    "FollowUpAppointmentId" uuid,
    "State" character varying(20) NOT NULL,
    "Revision" bigint NOT NULL,
    "CurrentOfferId" uuid,
    "PolicyVersion" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentWaitlistEntries" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_AppointmentWaitlistEntries_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_AppointmentWaitlistEntries_Appointments_TenantId_FollowUpAp~" FOREIGN KEY ("TenantId", "FollowUpAppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentWaitlistEntries_Facilities_TenantId_FacilityId" FOREIGN KEY ("TenantId", "FacilityId") REFERENCES "Facilities" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentWaitlistEntries_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentWaitlistEntries_Users_TenantId_PractitionerUserId" FOREIGN KEY ("TenantId", "PractitionerUserId") REFERENCES "Users" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "AppointmentWaitlistOffers" (
    "Id" uuid NOT NULL,
    "EntryId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "ReleasedReservationId" uuid NOT NULL,
    "ReservationId" uuid NOT NULL,
    "OwnerUserId" uuid NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "State" character varying(20) NOT NULL,
    "Revision" bigint NOT NULL,
    "AppointmentId" uuid,
    "PolicyVersion" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_AppointmentWaitlistOffers" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_AppointmentWaitlistOffers_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_AppointmentWaitlistOffers_AppointmentReservations_TenantId_~" FOREIGN KEY ("TenantId", "ReleasedReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentWaitlistOffers_AppointmentReservations_TenantId~1" FOREIGN KEY ("TenantId", "ReservationId") REFERENCES "AppointmentReservations" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentWaitlistOffers_AppointmentWaitlistEntries_Tenant~" FOREIGN KEY ("TenantId", "EntryId") REFERENCES "AppointmentWaitlistEntries" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentWaitlistOffers_Appointments_TenantId_Appointment~" FOREIGN KEY ("TenantId", "AppointmentId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentWaitlistOffers_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AppointmentWaitlistOffers_Users_TenantId_OwnerUserId" FOREIGN KEY ("TenantId", "OwnerUserId") REFERENCES "Users" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_AppointmentWaitlistDecisions_TenantId_EntryId_EntryRevision" ON "AppointmentWaitlistDecisions" ("TenantId", "EntryId", "EntryRevision");

CREATE INDEX "IX_AppointmentWaitlistDecisions_TenantId_OfferId" ON "AppointmentWaitlistDecisions" ("TenantId", "OfferId");

CREATE INDEX "IX_AppointmentWaitlistDecisions_TenantId_PatientId" ON "AppointmentWaitlistDecisions" ("TenantId", "PatientId");

CREATE INDEX "IX_AppointmentWaitlistEntries_TenantId_CurrentOfferId" ON "AppointmentWaitlistEntries" ("TenantId", "CurrentOfferId");

CREATE INDEX "IX_AppointmentWaitlistEntries_TenantId_FacilityId_State_Create~" ON "AppointmentWaitlistEntries" ("TenantId", "FacilityId", "State", "CreatedAt");

CREATE INDEX "IX_AppointmentWaitlistEntries_TenantId_FollowUpAppointmentId" ON "AppointmentWaitlistEntries" ("TenantId", "FollowUpAppointmentId");

CREATE INDEX "IX_AppointmentWaitlistEntries_TenantId_PatientId_State_Expires~" ON "AppointmentWaitlistEntries" ("TenantId", "PatientId", "State", "ExpiresAtUtc");

CREATE INDEX "IX_AppointmentWaitlistEntries_TenantId_PractitionerUserId" ON "AppointmentWaitlistEntries" ("TenantId", "PractitionerUserId");

CREATE INDEX "IX_AppointmentWaitlistOffers_TenantId_AppointmentId" ON "AppointmentWaitlistOffers" ("TenantId", "AppointmentId");

CREATE UNIQUE INDEX "IX_AppointmentWaitlistOffers_TenantId_EntryId" ON "AppointmentWaitlistOffers" ("TenantId", "EntryId") WHERE "State" = 'offered';

CREATE INDEX "IX_AppointmentWaitlistOffers_TenantId_OwnerUserId" ON "AppointmentWaitlistOffers" ("TenantId", "OwnerUserId");

CREATE INDEX "IX_AppointmentWaitlistOffers_TenantId_PatientId" ON "AppointmentWaitlistOffers" ("TenantId", "PatientId");

CREATE INDEX "IX_AppointmentWaitlistOffers_TenantId_ReleasedReservationId" ON "AppointmentWaitlistOffers" ("TenantId", "ReleasedReservationId");

CREATE UNIQUE INDEX "IX_AppointmentWaitlistOffers_TenantId_ReservationId" ON "AppointmentWaitlistOffers" ("TenantId", "ReservationId");

ALTER TABLE "AppointmentWaitlistDecisions" ADD CONSTRAINT "FK_AppointmentWaitlistDecisions_AppointmentWaitlistEntries_Ten~" FOREIGN KEY ("TenantId", "EntryId") REFERENCES "AppointmentWaitlistEntries" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "AppointmentWaitlistDecisions" ADD CONSTRAINT "FK_AppointmentWaitlistDecisions_AppointmentWaitlistOffers_Tena~" FOREIGN KEY ("TenantId", "OfferId") REFERENCES "AppointmentWaitlistOffers" ("TenantId", "Id") ON DELETE RESTRICT;

ALTER TABLE "AppointmentWaitlistEntries" ADD CONSTRAINT "FK_AppointmentWaitlistEntries_AppointmentWaitlistOffers_Tenant~" FOREIGN KEY ("TenantId", "CurrentOfferId") REFERENCES "AppointmentWaitlistOffers" ("TenantId", "Id") ON DELETE RESTRICT;

DO $rls$ DECLARE tbl text;
BEGIN
 FOREACH tbl IN ARRAY ARRAY['AppointmentWaitlistEntries','AppointmentWaitlistOffers','AppointmentWaitlistDecisions'] LOOP
  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',tbl);
  EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',tbl);
  EXECUTE format('CREATE POLICY tenant_scope ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',tbl);
  EXECUTE format('CREATE POLICY patient_scope ON %I AS RESTRICTIVE FOR SELECT USING(public.cureflow_patient_read_allowed("TenantId","PatientId"))',tbl);
 END LOOP;
END $rls$;
ALTER TABLE "AppointmentWaitlistEntries" ADD CONSTRAINT waitlist_entry_shape CHECK(COALESCE(
 "Revision">0 AND "IsActive" AND NOT "IsDeleted" AND "DurationMinutes" BETWEEN 1 AND 480
 AND "EarliestStartsAtUtc"<"LatestEndsAtUtc" AND "LatestEndsAtUtc"-"EarliestStartsAtUtc">=make_interval(mins=>"DurationMinutes")
 AND "ExpiresAtUtc"="LatestEndsAtUtc" AND "LatestEndsAtUtc"<="CreatedAt"+interval '365 days'
 AND jsonb_typeof("ResourceIdsJson"::jsonb)='array' AND jsonb_array_length("ResourceIdsJson"::jsonb)<=5
 AND length("PolicyVersion") BETWEEN 1 AND 100 AND "BookingKind" IN('routine','walk_in','follow_up')
 AND ("BookingKind"='follow_up')=("FollowUpAppointmentId" IS NOT NULL)
 AND (("State"='waiting' AND "CurrentOfferId" IS NULL) OR ("State" IN('offered','accepted') AND "CurrentOfferId" IS NOT NULL)
 OR "State" IN('withdrawn','expired')),false));
ALTER TABLE "AppointmentWaitlistOffers" ADD CONSTRAINT waitlist_offer_shape CHECK(COALESCE(
 "IsActive" AND NOT "IsDeleted" AND "CreatedBy"="OwnerUserId" AND "ExpiresAtUtc">"CreatedAt"
 AND "ExpiresAtUtc"<="CreatedAt"+interval '30 minutes' AND length("PolicyVersion") BETWEEN 1 AND 100
 AND "ReservationId"<>"ReleasedReservationId" AND (("State"='offered' AND "Revision"=1 AND "AppointmentId" IS NULL)
 OR("State"='accepted' AND "Revision"=2 AND "AppointmentId" IS NOT NULL)
 OR("State" IN('expired','withdrawn') AND "Revision"=2 AND "AppointmentId" IS NULL)),false));
ALTER TABLE "AppointmentWaitlistDecisions" ADD CONSTRAINT waitlist_decision_shape CHECK(
 "EntryRevision">0 AND "IsActive" AND NOT "IsDeleted" AND "CreatedBy" IS NOT NULL
 AND "Action" IN('add','offer','accept','expire_offer','withdrawn','expired') AND length("Reason") BETWEEN 3 AND 1000);
CREATE FUNCTION public.guard_appointment_waitlist_entry() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Waitlist request history is retained' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 IF NOT public.cureflow_patient_read_allowed(NEW."TenantId",NEW."PatientId")
  OR NULLIF(current_setting('app.current_user_id',true),'')::uuid IS NULL THEN RAISE EXCEPTION 'Current patient actor scope required' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  IF NEW."FollowUpAppointmentId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM public."Appointments" a
   WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."FollowUpAppointmentId" AND a."PatientId"=NEW."PatientId"
    AND a."ScheduledAt"<NEW."EarliestStartsAtUtc" AND a."Status" NOT IN(3,4) AND a."IsActive" AND NOT a."IsDeleted") THEN
   RAISE EXCEPTION 'Waitlist follow-up requires exact earlier same-patient source' USING ERRCODE='23514';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM public."Users" u JOIN public."StaffProfiles" p ON p."TenantId"=u."TenantId" AND p."UserId"=u."Id"
    WHERE u."TenantId"=NEW."TenantId" AND u."Id"=NEW."PractitionerUserId" AND u."Role" IN(1,4,6)
     AND u."IsActive" AND NOT u."IsDeleted" AND p."IsActive" AND NOT p."IsDeleted") THEN
   RAISE EXCEPTION 'Waitlist requires active eligible staff' USING ERRCODE='23514';
  END IF;
  IF NEW."Revision"<>1 OR NEW."State"<>'waiting' OR NEW."EarliestStartsAtUtc"<=clock_timestamp()
   OR public.cureflow_lock_active_facility(NEW."TenantId",NEW."FacilityId") IS NULL
   OR NOT EXISTS(SELECT 1 FROM public."PatientIdentifiers" i WHERE i."TenantId"=NEW."TenantId" AND i."PatientId"=NEW."PatientId"
    AND i."FacilityId"=NEW."FacilityId" AND i."Namespace"='local-mrn') THEN
   RAISE EXCEPTION 'Waitlist starts with an explicit current patient/facility request' USING ERRCODE='23514';
  END IF;
 ELSE
  IF NEW."Revision"<>OLD."Revision"+1 OR OLD."State" NOT IN('waiting','offered')
   OR (to_jsonb(OLD)-ARRAY['State','Revision','CurrentOfferId','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','CurrentOfferId','UpdatedAt'])
   OR NOT((OLD."State"='waiting' AND NEW."State" IN('offered','withdrawn','expired'))
    OR(OLD."State"='offered' AND NEW."State" IN('waiting','accepted','withdrawn','expired'))) THEN
   RAISE EXCEPTION 'Waitlist intent is immutable; only reviewed terminal/offer transitions are allowed' USING ERRCODE='23514';
  END IF;
  IF NEW."State" IN('offered','accepted') AND (NEW."ExpiresAtUtc"<=clock_timestamp() OR NOT EXISTS(
   SELECT 1 FROM public."AppointmentWaitlistOffers" o WHERE o."TenantId"=NEW."TenantId" AND o."Id"=NEW."CurrentOfferId"
    AND o."EntryId"=NEW."Id" AND o."PatientId"=NEW."PatientId" AND o."State"=CASE WHEN NEW."State"='offered' THEN 'offered' ELSE 'accepted' END)) THEN
   RAISE EXCEPTION 'Entry and exact current offer state must agree' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='expired' AND NEW."ExpiresAtUtc">clock_timestamp() THEN RAISE EXCEPTION 'Live request cannot expire' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_waitlist_entry_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentWaitlistEntries"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_waitlist_entry();
CREATE FUNCTION public.guard_appointment_waitlist_offer() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE entry_row public."AppointmentWaitlistEntries"%ROWTYPE; held public."AppointmentReservations"%ROWTYPE; released public."AppointmentReservations"%ROWTYPE;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Waitlist offer history is retained' USING ERRCODE='23514'; END IF;
 PERFORM pg_advisory_xact_lock(734291);
 SELECT * INTO entry_row FROM public."AppointmentWaitlistEntries" e WHERE e."TenantId"=NEW."TenantId" AND e."Id"=NEW."EntryId" FOR UPDATE;
 IF NOT FOUND OR entry_row."PatientId"<>NEW."PatientId" OR NOT public.cureflow_patient_read_allowed(NEW."TenantId",NEW."PatientId") THEN
  RAISE EXCEPTION 'Offer requires exact current patient request' USING ERRCODE='23514';
 END IF;
 SELECT * INTO held FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReservationId" FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION 'Offer requires exact reservation' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  SELECT * INTO released FROM public."AppointmentReservations" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."ReleasedReservationId";
  IF NOT FOUND OR released."State"<>'released' OR held."State"<>'held' OR held."OwnerUserId"<>NEW."OwnerUserId"
   OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
   OR entry_row."State"<>'waiting' OR entry_row."ExpiresAtUtc"<=clock_timestamp() OR NEW."State"<>'offered' OR NEW."Revision"<>1
   OR NEW."ExpiresAtUtc"<>held."HoldExpiresAtUtc" OR held."HoldExpiresAtUtc"<=clock_timestamp() OR held."StartsAtUtc"<=clock_timestamp()
   OR held."FacilityId"<>entry_row."FacilityId" OR held."PractitionerUserId"<>entry_row."PractitionerUserId"
   OR (held."FacilityId",held."PractitionerUserId",held."StartsAtUtc",held."EndsAtUtc")
     IS DISTINCT FROM (released."FacilityId",released."PractitionerUserId",released."StartsAtUtc",released."EndsAtUtc")
   OR held."StartsAtUtc"<entry_row."EarliestStartsAtUtc" OR held."EndsAtUtc">entry_row."LatestEndsAtUtc"
   OR EXTRACT(EPOCH FROM(held."EndsAtUtc"-held."StartsAtUtc"))/60<>entry_row."DurationMinutes"
   OR EXISTS(SELECT 1 FROM jsonb_array_elements_text((entry_row."ResourceIdsJson"::jsonb)) ids(value) WHERE NOT EXISTS(
    SELECT 1 FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=held."Id"
     AND s."TargetType"='resource' AND s."TargetId"=ids.value::uuid))
   OR (SELECT count(*) FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=held."Id"
     AND s."TargetType"='resource')<>jsonb_array_length((entry_row."ResourceIdsJson"::jsonb))
   OR EXISTS((SELECT s."TargetType",s."TargetId" FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=held."Id")
     EXCEPT (SELECT s."TargetType",s."TargetId" FROM public."AppointmentReservationSeats" s WHERE s."TenantId"=NEW."TenantId" AND s."ReservationId"=released."Id")) THEN
   RAISE EXCEPTION 'Offer must bind exact released capacity and live owned hold' USING ERRCODE='23514';
  END IF;
 ELSE
  IF OLD."State"<>'offered' OR NEW."State" NOT IN('accepted','expired','withdrawn') OR NEW."Revision"<>2
   OR (to_jsonb(OLD)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['State','Revision','AppointmentId','UpdatedAt']) THEN
   RAISE EXCEPTION 'Offer evidence is immutable; decisions are terminal' USING ERRCODE='23514';
  END IF;
  IF NEW."State"='accepted' THEN
   IF OLD."ExpiresAtUtc"<=clock_timestamp() OR NEW."OwnerUserId" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
    OR held."State"<>'confirmed' OR held."AppointmentId"<>NEW."AppointmentId"
    OR NOT EXISTS(SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."AppointmentId"
     AND a."PatientId"=NEW."PatientId" AND a."ReservationId"=held."Id" AND a."FacilityId"=entry_row."FacilityId") THEN
    RAISE EXCEPTION 'Expired/changed offer cannot accept or bind another patient' USING ERRCODE='23514';
   END IF;
  ELSIF held."State" NOT IN('expired','released') OR (NEW."State"='expired' AND OLD."ExpiresAtUtc">clock_timestamp()) THEN
   RAISE EXCEPTION 'Offer closure requires exact hold release/expiry' USING ERRCODE='23514';
  END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_waitlist_offer_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentWaitlistOffers"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_waitlist_offer();
CREATE FUNCTION public.guard_appointment_waitlist_decision() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Waitlist decision evidence is immutable' USING ERRCODE='23514'; END IF;
 IF NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid OR NOT EXISTS(
  SELECT 1 FROM public."AppointmentWaitlistEntries" e WHERE e."TenantId"=NEW."TenantId" AND e."Id"=NEW."EntryId"
   AND e."PatientId"=NEW."PatientId" AND e."Revision"=NEW."EntryRevision") THEN
  RAISE EXCEPTION 'Decision requires exact current patient/request version and actor' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER appointment_waitlist_decision_guard BEFORE INSERT OR UPDATE OR DELETE ON "AppointmentWaitlistDecisions"
 FOR EACH ROW EXECUTE FUNCTION public.guard_appointment_waitlist_decision();
CREATE FUNCTION public.require_waitlist_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE entry_id uuid; scope_tenant uuid; entry_row public."AppointmentWaitlistEntries"%ROWTYPE;
BEGIN
 IF TG_TABLE_NAME='AppointmentWaitlistEntries' THEN entry_id=NEW."Id"; ELSE entry_id=NEW."EntryId"; END IF;
 scope_tenant=NEW."TenantId";
 SELECT * INTO entry_row FROM public."AppointmentWaitlistEntries" e WHERE e."TenantId"=scope_tenant AND e."Id"=entry_id;
 IF NOT FOUND OR NOT EXISTS(SELECT 1 FROM public."AppointmentWaitlistDecisions" d WHERE d."TenantId"=scope_tenant AND d."EntryId"=entry_id
    AND d."EntryRevision"=entry_row."Revision"
    AND ((entry_row."State"='waiting' AND d."Action" IN('add','expire_offer')) OR(entry_row."State"='offered' AND d."Action"='offer')
      OR(entry_row."State"='accepted' AND d."Action"='accept') OR(entry_row."State" IN('expired','withdrawn') AND d."Action"=entry_row."State")))
  OR EXISTS(SELECT 1 FROM public."AppointmentWaitlistOffers" o WHERE o."TenantId"=scope_tenant AND o."EntryId"=entry_id AND o."State"='offered'
    AND (entry_row."State"<>'offered' OR entry_row."CurrentOfferId"<>o."Id"))
  OR (entry_row."State"='offered' AND NOT EXISTS(SELECT 1 FROM public."AppointmentWaitlistOffers" o
    WHERE o."TenantId"=scope_tenant AND o."Id"=entry_row."CurrentOfferId" AND o."EntryId"=entry_id AND o."State"='offered'))
  OR (entry_row."State"='accepted' AND NOT EXISTS(SELECT 1 FROM public."AppointmentWaitlistOffers" o
    JOIN public."Appointments" a ON a."TenantId"=o."TenantId" AND a."Id"=o."AppointmentId"
    WHERE o."TenantId"=scope_tenant AND o."Id"=entry_row."CurrentOfferId" AND o."EntryId"=entry_id AND o."State"='accepted'
     AND a."PatientId"=entry_row."PatientId" AND a."ReservationId"=o."ReservationId")) THEN
  RAISE EXCEPTION 'Waitlist offer, acceptance booking and accountable decision must commit together' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER waitlist_entry_commit AFTER INSERT OR UPDATE ON "AppointmentWaitlistEntries" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_waitlist_commit();
CREATE CONSTRAINT TRIGGER waitlist_offer_commit AFTER INSERT OR UPDATE ON "AppointmentWaitlistOffers" DEFERRABLE INITIALLY DEFERRED
 FOR EACH ROW EXECUTE FUNCTION public.require_waitlist_commit();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "AppointmentWaitlistEntries","AppointmentWaitlistOffers","AppointmentWaitlistDecisions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "AppointmentWaitlistEntries","AppointmentWaitlistOffers","AppointmentWaitlistDecisions" FROM cureflow_tenant;
 GRANT UPDATE("State","Revision","CurrentOfferId","UpdatedAt") ON "AppointmentWaitlistEntries" TO cureflow_tenant;
 GRANT UPDATE("State","Revision","AppointmentId","UpdatedAt") ON "AppointmentWaitlistOffers" TO cureflow_tenant;
END IF; END $grant$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009115713_AddReleasedCapacityWaitlists', '10.0.0');

COMMIT;

START TRANSACTION;
CREATE TABLE "ClinicalFollowUpWorks" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "SourceType" character varying(30) NOT NULL,
    "SourceId" uuid NOT NULL,
    "SourceVersion" character varying(30) NOT NULL,
    "AssignedUserId" uuid,
    "EscalationOwnerUserId" uuid NOT NULL,
    "DueAtUtc" timestamp with time zone NOT NULL,
    "State" character varying(20) NOT NULL,
    "Revision" bigint NOT NULL,
    "AcknowledgedAtUtc" timestamp with time zone,
    "CompletedAtUtc" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ClinicalFollowUpWorks" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_ClinicalFollowUpWorks_TenantId_Id" UNIQUE ("TenantId", "Id"),
    CONSTRAINT "FK_ClinicalFollowUpWorks_Appointments_TenantId_SourceId" FOREIGN KEY ("TenantId", "SourceId") REFERENCES "Appointments" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ClinicalFollowUpWorks_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ClinicalFollowUpWorks_Users_TenantId_AssignedUserId" FOREIGN KEY ("TenantId", "AssignedUserId") REFERENCES "Users" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ClinicalFollowUpWorks_Users_TenantId_EscalationOwnerUserId" FOREIGN KEY ("TenantId", "EscalationOwnerUserId") REFERENCES "Users" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "ClinicalFollowUpVersions" (
    "Id" uuid NOT NULL,
    "WorkId" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "Revision" bigint NOT NULL,
    "AssignedUserId" uuid,
    "EscalationOwnerUserId" uuid NOT NULL,
    "DueAtUtc" timestamp with time zone NOT NULL,
    "State" character varying(20) NOT NULL,
    "AcknowledgedAtUtc" timestamp with time zone,
    "CompletedAtUtc" timestamp with time zone,
    "Action" character varying(30) NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "EvidenceNoteId" uuid,
    "EvidenceNoteRevision" integer,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_ClinicalFollowUpVersions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ClinicalFollowUpVersions_ClinicalFollowUpWorks_TenantId_Wor~" FOREIGN KEY ("TenantId", "WorkId") REFERENCES "ClinicalFollowUpWorks" ("TenantId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ClinicalFollowUpVersions_Patients_TenantId_PatientId" FOREIGN KEY ("TenantId", "PatientId") REFERENCES "Patients" ("TenantId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ClinicalFollowUpVersions_TenantId_PatientId" ON "ClinicalFollowUpVersions" ("TenantId", "PatientId");

CREATE UNIQUE INDEX "IX_ClinicalFollowUpVersions_TenantId_WorkId_Revision" ON "ClinicalFollowUpVersions" ("TenantId", "WorkId", "Revision");

CREATE INDEX "IX_ClinicalFollowUpWorks_TenantId_AssignedUserId" ON "ClinicalFollowUpWorks" ("TenantId", "AssignedUserId");

CREATE INDEX "IX_ClinicalFollowUpWorks_TenantId_EscalationOwnerUserId_State_~" ON "ClinicalFollowUpWorks" ("TenantId", "EscalationOwnerUserId", "State", "DueAtUtc");

CREATE INDEX "IX_ClinicalFollowUpWorks_TenantId_PatientId" ON "ClinicalFollowUpWorks" ("TenantId", "PatientId");

CREATE INDEX "IX_ClinicalFollowUpWorks_TenantId_SourceId" ON "ClinicalFollowUpWorks" ("TenantId", "SourceId");

DO $rls$ DECLARE tbl text; BEGIN
 FOREACH tbl IN ARRAY ARRAY['ClinicalFollowUpWorks','ClinicalFollowUpVersions'] LOOP
  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',tbl);
  EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY',tbl);
  EXECUTE format('CREATE POLICY tenant_scope ON %I USING ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid) WITH CHECK ("TenantId"=NULLIF(current_setting(''app.current_tenant_id'',true),'''')::uuid)',tbl);
  EXECUTE format('CREATE POLICY patient_scope ON %I AS RESTRICTIVE FOR SELECT USING(public.cureflow_patient_read_allowed("TenantId","PatientId"))',tbl);
 END LOOP;
END $rls$;
INSERT INTO "Permissions"("Id","Code","Name","Description","CreatedAt","UpdatedAt","IsActive","IsDeleted")
 SELECT p.id::uuid,p.code,p.code,'Explicitly reviewed clinical follow-up capability; no automatic grants',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,true,false
 FROM(VALUES('54fa1070-d201-41ce-9000-000000000001','FollowUp.View'),('54fa1070-d201-41ce-9000-000000000002','FollowUp.Manage'),
 ('54fa1070-d201-41ce-9000-000000000003','FollowUp.Complete'),('54fa1070-d201-41ce-9000-000000000004','FollowUp.Escalate'))p(id,code)
 WHERE NOT EXISTS(SELECT 1 FROM "Permissions" e WHERE e."Code"=p.code);
CREATE FUNCTION public.cureflow_follow_up_user_allowed(scope_tenant uuid,scope_user uuid,scope_patient uuid,capability text)
 RETURNS boolean LANGUAGE sql STABLE AS $authority$
 SELECT EXISTS(SELECT 1 FROM public."Users" u WHERE u."TenantId"=scope_tenant AND u."Id"=scope_user AND u."IsActive" AND NOT u."IsDeleted")
 AND NOT EXISTS(SELECT required.code FROM(VALUES(capability),('FollowUp.View'),('Clinical.View'),('Patient.View'))required(code)
  WHERE NOT EXISTS(SELECT 1 FROM public."UserRoleAssignments" a
   JOIN public."Roles" r ON r."Id"=a."RoleId" AND r."IsActive" AND NOT r."IsDeleted"
   JOIN public."RolePermissions" rp ON rp."RoleId"=r."Id" AND rp."IsActive" AND NOT rp."IsDeleted"
   JOIN public."Permissions" p ON p."Id"=rp."PermissionId" AND p."IsActive" AND NOT p."IsDeleted"
   WHERE a."TenantId"=scope_tenant AND a."UserId"=scope_user AND a."IsActive" AND NOT a."IsDeleted" AND p."Code"=required.code))
 AND (NOT EXISTS(SELECT 1 FROM public."PatientConfidentialityStates" c WHERE c."TenantId"=scope_tenant AND c."PatientId"=scope_patient AND c."Restricted")
 OR EXISTS(SELECT 1 FROM public."UserRoleAssignments" a JOIN public."Roles" r ON r."Id"=a."RoleId" AND r."IsActive" AND NOT r."IsDeleted"
  JOIN public."RolePermissions" rp ON rp."RoleId"=r."Id" AND rp."IsActive" AND NOT rp."IsDeleted"
  JOIN public."Permissions" p ON p."Id"=rp."PermissionId" AND p."IsActive" AND NOT p."IsDeleted"
  WHERE a."TenantId"=scope_tenant AND a."UserId"=scope_user AND a."IsActive" AND NOT a."IsDeleted" AND p."Code"='PatientRestricted.View'));
$authority$;
ALTER TABLE "ClinicalFollowUpWorks" ADD CONSTRAINT named_follow_up_shape CHECK(COALESCE(
 "IsActive" AND NOT "IsDeleted" AND "SourceType"='appointment' AND "SourceVersion"~'^[1-9][0-9]*$' AND "Revision">0
 AND (("State"='open' AND "AcknowledgedAtUtc" IS NULL AND "CompletedAtUtc" IS NULL)
 OR("State"='acknowledged' AND "AssignedUserId" IS NOT NULL AND "AcknowledgedAtUtc" IS NOT NULL AND "CompletedAtUtc" IS NULL)
 OR("State"='completed' AND "AssignedUserId" IS NOT NULL AND "AcknowledgedAtUtc" IS NOT NULL AND "CompletedAtUtc">="AcknowledgedAtUtc")),false));
ALTER TABLE "ClinicalFollowUpVersions" ADD CONSTRAINT named_follow_up_version_shape CHECK(COALESCE(
 "IsActive" AND NOT "IsDeleted" AND "Revision">0 AND length(btrim("Reason")) BETWEEN 3 AND 1000
 AND "Action" IN('create','acknowledge','reassign','complete') AND ("Action"='complete')=("EvidenceNoteId" IS NOT NULL AND "EvidenceNoteRevision">0),false));
CREATE FUNCTION public.guard_named_follow_up() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE actor uuid; cap text;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Clinical follow-up work is retained' USING ERRCODE='23514'; END IF;
 actor=NULLIF(current_setting('app.current_user_id',true),'')::uuid;
 IF actor IS NULL OR NOT public.cureflow_patient_read_allowed(NEW."TenantId",NEW."PatientId") THEN
  RAISE EXCEPTION 'Current clinical chart authority required' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' THEN
  cap='FollowUp.Manage';
  IF NEW."CreatedBy" IS DISTINCT FROM actor OR NEW."Revision"<>1 OR NEW."State"<>'open' OR NOT EXISTS(
   SELECT 1 FROM public."Appointments" a WHERE a."TenantId"=NEW."TenantId" AND a."Id"=NEW."SourceId"
    AND a."PatientId"=NEW."PatientId" AND a."Revision"::text=NEW."SourceVersion" AND a."IsActive" AND NOT a."IsDeleted") THEN
   RAISE EXCEPTION 'Follow-up requires exact current appointment source and actor' USING ERRCODE='23514'; END IF;
 ELSE
  IF (NEW."TenantId",NEW."Id",NEW."PatientId",NEW."SourceType",NEW."SourceId",NEW."SourceVersion",NEW."CreatedAt",NEW."CreatedBy",NEW."IsActive",NEW."IsDeleted")
    IS DISTINCT FROM(OLD."TenantId",OLD."Id",OLD."PatientId",OLD."SourceType",OLD."SourceId",OLD."SourceVersion",OLD."CreatedAt",OLD."CreatedBy",OLD."IsActive",OLD."IsDeleted")
   OR NEW."Revision"<>OLD."Revision"+1 OR NEW."UpdatedBy" IS DISTINCT FROM actor OR OLD."State"='completed' THEN
   RAISE EXCEPTION 'Follow-up source is retained and mutations need exact next version' USING ERRCODE='23514'; END IF;
  IF NEW."State"='open' THEN cap='FollowUp.Manage';
  ELSE
   cap='FollowUp.Complete';
   IF OLD."AssignedUserId" IS DISTINCT FROM actor
    OR(NEW."AssignedUserId",NEW."EscalationOwnerUserId",NEW."DueAtUtc") IS DISTINCT FROM(OLD."AssignedUserId",OLD."EscalationOwnerUserId",OLD."DueAtUtc")
    OR(NEW."State"='acknowledged' AND (OLD."State"<>'open' OR NEW."AcknowledgedAtUtc" IS NULL))
    OR(NEW."State"='completed' AND (OLD."State"<>'acknowledged' OR NEW."AcknowledgedAtUtc" IS DISTINCT FROM OLD."AcknowledgedAtUtc")) THEN
    RAISE EXCEPTION 'Acknowledgment/completion requires current named assignee and allowed transition' USING ERRCODE='23514'; END IF;
  END IF;
 END IF;
 IF NOT public.cureflow_follow_up_user_allowed(NEW."TenantId",actor,NEW."PatientId",cap) THEN
  RAISE EXCEPTION 'Current follow-up capability required' USING ERRCODE='23514'; END IF;
 IF TG_OP='INSERT' OR NEW."State"='open' THEN
  IF NEW."DueAtUtc"<=clock_timestamp() OR NEW."DueAtUtc">clock_timestamp()+interval '365 days'
   OR NOT public.cureflow_follow_up_user_allowed(NEW."TenantId",NEW."EscalationOwnerUserId",NEW."PatientId",'FollowUp.Escalate')
   OR(NEW."AssignedUserId" IS NOT NULL AND NOT public.cureflow_follow_up_user_allowed(NEW."TenantId",NEW."AssignedUserId",NEW."PatientId",'FollowUp.Complete')) THEN
   RAISE EXCEPTION 'Current named owner capabilities and bounded future due time required' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER named_follow_up_guard BEFORE INSERT OR UPDATE OR DELETE ON "ClinicalFollowUpWorks" FOR EACH ROW EXECUTE FUNCTION public.guard_named_follow_up();
CREATE FUNCTION public.guard_named_follow_up_version() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE w public."ClinicalFollowUpWorks"%ROWTYPE;
BEGIN
 IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Clinical follow-up version evidence is immutable' USING ERRCODE='23514'; END IF;
 SELECT * INTO w FROM public."ClinicalFollowUpWorks" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."WorkId" FOR SHARE;
 IF NOT FOUND OR NEW."CreatedBy" IS DISTINCT FROM NULLIF(current_setting('app.current_user_id',true),'')::uuid
  OR(NEW."PatientId",NEW."Revision",NEW."AssignedUserId",NEW."EscalationOwnerUserId",NEW."DueAtUtc",NEW."State",NEW."AcknowledgedAtUtc",NEW."CompletedAtUtc")
    IS DISTINCT FROM(w."PatientId",w."Revision",w."AssignedUserId",w."EscalationOwnerUserId",w."DueAtUtc",w."State",w."AcknowledgedAtUtc",w."CompletedAtUtc")
  OR NOT((NEW."Revision"=1 AND NEW."Action"='create') OR(NEW."Revision">1 AND NEW."Action"='reassign' AND w."State"='open')
    OR(NEW."Action"='acknowledge' AND w."State"='acknowledged') OR(NEW."Action"='complete' AND w."State"='completed')) THEN
  RAISE EXCEPTION 'Follow-up evidence must match exact attributable work version/action' USING ERRCODE='23514'; END IF;
 IF NEW."Action"='complete' AND NOT EXISTS(SELECT 1 FROM public."ClinicalNotes" n WHERE n."TenantId"=NEW."TenantId" AND n."Id"=NEW."EvidenceNoteId"
   AND n."PatientId"=w."PatientId" AND n."AppointmentId"=w."SourceId" AND n."Revision"=NEW."EvidenceNoteRevision"
   AND n."FinalizedAt" IS NOT NULL AND n."EditableUntil"<=clock_timestamp() AND n."IsActive" AND NOT n."IsDeleted") THEN
  RAISE EXCEPTION 'Completion requires exact finalized clinical note evidence' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER named_follow_up_version_guard BEFORE INSERT OR UPDATE OR DELETE ON "ClinicalFollowUpVersions" FOR EACH ROW EXECUTE FUNCTION public.guard_named_follow_up_version();
CREATE FUNCTION public.require_named_follow_up_commit() RETURNS trigger LANGUAGE plpgsql AS $guard$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM public."ClinicalFollowUpVersions" v WHERE v."TenantId"=NEW."TenantId" AND v."WorkId"=NEW."Id" AND v."Revision"=NEW."Revision"
  AND(v."PatientId",v."AssignedUserId",v."EscalationOwnerUserId",v."DueAtUtc",v."State",v."AcknowledgedAtUtc",v."CompletedAtUtc")
   IS NOT DISTINCT FROM(NEW."PatientId",NEW."AssignedUserId",NEW."EscalationOwnerUserId",NEW."DueAtUtc",NEW."State",NEW."AcknowledgedAtUtc",NEW."CompletedAtUtc")) THEN
  RAISE EXCEPTION 'Follow-up state and attributable version must commit together' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER named_follow_up_commit AFTER INSERT OR UPDATE ON "ClinicalFollowUpWorks" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.require_named_follow_up_commit();
DO $grant$ BEGIN IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='cureflow_tenant') THEN
 GRANT SELECT,INSERT ON "ClinicalFollowUpWorks","ClinicalFollowUpVersions" TO cureflow_tenant;
 REVOKE UPDATE,DELETE ON "ClinicalFollowUpWorks","ClinicalFollowUpVersions" FROM cureflow_tenant;
 GRANT UPDATE("AssignedUserId","EscalationOwnerUserId","DueAtUtc","State","Revision","AcknowledgedAtUtc","CompletedAtUtc","UpdatedAt","UpdatedBy") ON "ClinicalFollowUpWorks" TO cureflow_tenant;
END IF; END $grant$;


INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261012110709_AddNamedClinicalFollowUps', '10.0.0');

COMMIT;

