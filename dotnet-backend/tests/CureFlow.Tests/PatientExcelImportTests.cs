using System.Data;
using System.IO.Compression;
using System.Text;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Services;
using ExcelDataReader;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CureFlow.Tests;

public class PatientExcelImportTests
{
    [Fact]
    public void PhoneNormalization_WorksForImportedRows()
    {
        var rawPhone = " 9876543210 ";
        var normalized = WhatsappPhoneHelper.Normalize(rawPhone);
        normalized.Should().Be("919876543210");
    }

    [Fact]
    public void CSV_ParsingHeaders_IdentifiesRequiredColumns()
    {
        var csvContent = "Name,Phone,Age,Gender,Department\nJohn Doe,9876543210,30,Male,Cardiology";
        var lines = csvContent.Split('\n');
        var header = lines[0].Split(',').Select(h => h.Trim().ToLower()).ToArray();

        header.Should().Contain("name");
        header.Should().Contain("phone");
        Array.IndexOf(header, "name").Should().Be(0);
        Array.IndexOf(header, "phone").Should().Be(1);
    }

    [Fact]
    public async Task CSV_Import_Ignores_Duplicate_Rows_With_Same_Name_Phone_Age_Gender()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        db.Setup(x => x.TransactionAsync(It.IsAny<Func<ICureFlowDbSession, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ICureFlowDbSession, Task>, CancellationToken>(async (action, ct) => await action(db.Object));

        var audit = Mock.Of<IAuditService>();
        var notifications = Mock.Of<INotificationPublisher>();
        var tenant = Mock.Of<ITenantContext>(t => t.UserId == Guid.NewGuid());
        var logger = Mock.Of<ILogger<PatientService>>();

        var service = new PatientService(db.Object, audit, notifications, tenant, logger);

        var csv = new MemoryStream(Encoding.UTF8.GetBytes(
            "Name,Phone,Age,Gender,Department\n" +
            "Alice,9876543210,30,Female,General\n" +
            "Alice,9876543210,30,Female,General\n"));

        var result = await service.ImportCsvAsync(csv);

        result.Inserted.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.SkipLog.Should().ContainSingle(s => s.Reason.Contains("Duplicate patient in file"));
        db.Verify(x => x.InsertAsync(It.IsAny<Patient>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CSV_Import_Allows_Row_Without_Phone()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        db.Setup(x => x.TransactionAsync(It.IsAny<Func<ICureFlowDbSession, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ICureFlowDbSession, Task>, CancellationToken>(async (action, ct) => await action(db.Object));

        var audit = Mock.Of<IAuditService>();
        var notifications = Mock.Of<INotificationPublisher>();
        var tenant = Mock.Of<ITenantContext>(t => t.UserId == Guid.NewGuid());
        var logger = Mock.Of<ILogger<PatientService>>();

        var service = new PatientService(db.Object, audit, notifications, tenant, logger);

        var csv = new MemoryStream(Encoding.UTF8.GetBytes(
            "Name,Phone,Age,Gender,Department\n" +
            "Alice,,30,Female,General\n" +
            "Alice,,30,Female,General\n"));

        var result = await service.ImportCsvAsync(csv);

        result.Inserted.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.SkipLog.Should().ContainSingle(s => s.Reason.Contains("Duplicate patient in file"));
        db.Verify(x => x.InsertAsync(It.IsAny<Patient>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task CSV_Import_Preserves_Quoted_Template_Fields_And_Multiline_Notes()
    {
        var db = new Mock<ICureFlowDbSession>();
        db.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        db.Setup(x => x.TransactionAsync(It.IsAny<Func<ICureFlowDbSession, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ICureFlowDbSession, Task>, CancellationToken>(async (action, ct) => await action(db.Object));
        Patient? inserted = null;
        db.Setup(x => x.InsertAsync(It.IsAny<Patient>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Patient, bool, CancellationToken>((patient, _, _) => inserted = patient)
            .Returns(Task.CompletedTask);
        var service = new PatientService(db.Object, Mock.Of<IAuditService>(), Mock.Of<INotificationPublisher>(),
            Mock.Of<ITenantContext>(t => t.UserId == Guid.NewGuid()), Mock.Of<ILogger<PatientService>>());
        // Same quoting and UTF-8 BOM as the Nuxt template/export helper.
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(
            "\uFEFF\"Patient Name\",\"Mobile Number\",\"Email\",\"Age\",\"Gender\",\"Department\",\"Tags\",\"Notes\"\r\n" +
            "\"Doe, Jane\",\"9876543210\",\"jane@example.test\",\"45\",\"Female\",\"General\",\"VIP;Follow-up\",\"First line\r\nSaid \"\"hello\"\"\"\r\n"));
        var result = await service.ImportExcelAsync(csv, ".csv");
        result.Inserted.Should().Be(1);
        result.Skipped.Should().Be(0);
        inserted.Should().NotBeNull();
        inserted!.Name.Should().Be("Doe, Jane");
        inserted.Phone.Should().Be("919876543210");
        inserted.Email.Should().Be("jane@example.test");
        inserted.Age.Should().Be(45);
        inserted.Gender.Should().Be(CureFlow.Domain.Enums.Gender.Female);
        inserted.Tags.Should().Equal("VIP", "Follow-up");
        inserted.Notes.Should().Contain("First line").And.Contain("Said \"hello\"");
    }
}
