using System.Globalization;
using System.IO.Compression;
using System.Text;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Enums;
using ExcelDataReader;
namespace CureFlow.Infrastructure.Imports;
internal record ParsedPatientImportRow(int RowNumber,string[] RawValues,PatientImportDraft? Draft,string[] Errors,bool RepeatedFileRow);
internal static class PatientImportParser
{
    public const long MaximumBytes=8*1024*1024;
    public const int MaximumRows=5000;
    private static readonly HashSet<string> Fields=new(StringComparer.Ordinal){"name","phone","email","age","gender","birth_date"};
    public static void ValidateMapping(PatientImportMapping mapping)
    {
        if(mapping.FacilityId==Guid.Empty||mapping.HeaderRow is <1 or >5||mapping.Worksheet is <0 or >9
            ||mapping.Columns is null||mapping.Columns.Count is <1 or >6||!mapping.Columns.ContainsKey("name")
            ||mapping.Columns.Any(c=>!Fields.Contains(c.Key)||c.Value is <0 or >=128)
            ||mapping.Columns.Values.Distinct().Count()!=mapping.Columns.Count
            ||mapping.DateFormat is not ("yyyy-MM-dd" or "dd/MM/yyyy" or "MM/dd/yyyy")
            ||mapping.CsvSeparator is not ("comma" or "semicolon" or "tab"))
            throw new ValidationException("Supply an explicit facility, header/column mapping and date/delimiter format.");
    }
    public static List<ParsedPatientImportRow> Parse(byte[] bytes,string format,PatientImportMapping mapping,CancellationToken ct)
    {
        ValidateMapping(mapping);
        if(bytes.Length==0||bytes.Length>MaximumBytes||format is not ("csv" or "xls" or "xlsx"))throw new ValidationException("Supply a bounded CSV/XLS/XLSX file.");
        var rows=new List<ParsedPatientImportRow>();var seen=new HashSet<string>(StringComparer.Ordinal);
        var totalCharacters=0;var physicalRow=0;
        void Add(object?[] cells)
        {
            ct.ThrowIfCancellationRequested();physicalRow++;
            if(cells.Length>128||physicalRow>MaximumRows+mapping.HeaderRow)throw new DomainException("Import exceeds 5000 data rows or 128 columns.",413);
            var raw=cells.Select(Raw).ToArray();
            if(raw.Any(v=>v.Length>4096))throw new DomainException("Import cell exceeds 4096 characters.",413);
            totalCharacters=checked(totalCharacters+raw.Sum(v=>v.Length));
            if(totalCharacters>8*1024*1024)throw new DomainException("Import parsed text exceeds its permitted size.",413);
            if(physicalRow<=mapping.HeaderRow)return;
            if(raw.All(string.IsNullOrWhiteSpace))return;
            var errors=new List<string>();
            object? Cell(string field)=>mapping.Columns.TryGetValue(field,out var index)&&index<cells.Length?cells[index]:null;
            string Value(string field)=>Raw(Cell(field)).Trim();
            var name=Value("name");var phone=Value("phone");var email=Value("email");var ageText=Value("age");var genderText=Value("gender");var birth=Value("birth_date");
            if(mapping.Columns.Values.Any(index=>index>=cells.Length))errors.Add("mapped_column_missing");
            if(string.IsNullOrWhiteSpace(name)||name.Length>200||name.Any(char.IsControl))errors.Add("name_invalid");
            if(phone.Length!=0)
            {
                if(Cell("phone") is not string)errors.Add("phone_numeric_precision_requires_review");
                if(!phone.StartsWith('+')||phone.Any(c=>!char.IsAsciiDigit(c)&&c is not '+' and not ' ' and not '-' and not '(' and not ')'))
                    errors.Add("phone_explicit_country_prefix_required");
                var digits=new string(phone.Where(char.IsAsciiDigit).ToArray());
                if(digits.Length is <7 or >15||digits.StartsWith('0')||phone.Count(c=>c=='+')!=1)errors.Add("phone_invalid");
                phone="+"+digits; // Retain explicit country context; do not infer the local 91 prefix.
            }
            if(email.Length!=0&&(email.Length>254||!System.Net.Mail.MailAddress.TryCreate(email,out _)))errors.Add("email_invalid");
            int? age=null;
            if(ageText.Length!=0)
            {
                if(decimal.TryParse(ageText,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var parsed)&&parsed==decimal.Truncate(parsed)&&parsed is >=0 and <=150)age=(int)parsed;
                else errors.Add("age_invalid");
            }
            var gender=Gender.Unknown;
            if(genderText.Length!=0)
            {
                gender=genderText.ToLowerInvariant() switch{"m" or "male" or "पुरुष"=>Gender.Male,"f" or "female" or "महिला"=>Gender.Female,"o" or "other"=>Gender.Other,_=>Gender.Unknown};
                if(gender==Gender.Unknown&&!genderText.Equals("unknown",StringComparison.OrdinalIgnoreCase))errors.Add("gender_requires_review");
            }
            BirthDateEvidence? birthDate=null;
            if(birth.Length!=0)
            {
                DateOnly date;
                var valid=Cell("birth_date") is DateTime excelDate
                    ?(date=DateOnly.FromDateTime(excelDate))!=default
                    :DateOnly.TryParseExact(birth,mapping.DateFormat,CultureInfo.InvariantCulture,DateTimeStyles.None,out date);
                if(!valid||date>DateOnly.FromDateTime(DateTime.UtcNow))errors.Add("birth_date_requires_explicit_format_review");
                else birthDate=new("day",date.Year,date.Month,date.Day);
            }
            var draft=new PatientImportDraft(name,phone,email.Length==0?null:email,age,gender,birthDate);
            var repeated=!seen.Add(System.Text.Json.JsonSerializer.Serialize(draft));
            rows.Add(new(physicalRow,raw,errors.Count==0?draft:null,errors.Distinct().ToArray(),repeated));
        }
        if(format=="csv")
        {
            var text=new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF');
            var delimiter=mapping.CsvSeparator switch{"semicolon"=>';',"tab"=>'\t',_=>','};
            var values=new List<object?>();var cell=new StringBuilder();var quoted=false;var closed=false;
            void FinishCell(){values.Add(cell.ToString());cell.Clear();closed=false;if(values.Count>128)throw new DomainException("Import exceeds 128 columns.",413);}
            for(var i=0;i<text.Length;i++)
            {
                if((i&4095)==0)ct.ThrowIfCancellationRequested();
                var c=text[i];
                if(quoted)
                {
                    if(c=='"'){if(i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else{quoted=false;closed=true;}}
                    else cell.Append(c);
                }
                else if(c==delimiter){FinishCell();}
                else if(c is '\r' or '\n')
                {
                    if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;
                    FinishCell();Add(values.ToArray());values.Clear();
                }
                else if(c=='"')
                {
                    if(cell.Length!=0||closed)throw new ValidationException("CSV quote structure is invalid.");
                    quoted=true;
                }
                else
                {
                    if(closed)throw new ValidationException("CSV quote structure is invalid.");
                    cell.Append(c);
                }
                if(cell.Length>4096)throw new DomainException("Import cell exceeds 4096 characters.",413);
            }
            if(quoted)throw new ValidationException("CSV quoted field is incomplete.");
            if(cell.Length!=0||values.Count!=0||closed){FinishCell();Add(values.ToArray());}
        }
        else
        {
            if(format=="xlsx")ValidateArchive(bytes,ct);
            else if(bytes.Length<8||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{0xD0,0xCF,0x11,0xE0,0xA1,0xB1,0x1A,0xE1}))throw new ValidationException("XLS file signature is invalid.");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var stream=new MemoryStream(bytes,false);
            using var reader=ExcelReaderFactory.CreateReader(stream);
            for(var sheet=0;sheet<mapping.Worksheet;sheet++)if(!reader.NextResult())throw new ValidationException("Selected worksheet does not exist.");
            while(reader.Read())
            {
                if(reader.FieldCount>128)throw new DomainException("Import exceeds 128 columns.",413);
                Add(Enumerable.Range(0,reader.FieldCount).Select(reader.GetValue).ToArray());
            }
        }
        if(physicalRow<mapping.HeaderRow)throw new ValidationException("Selected header row does not exist.");
        return rows;
    }
    private static string Raw(object? value)=>value switch{null or DBNull=>"",DateTime date=>date.ToString("O",CultureInfo.InvariantCulture),IFormattable form=>form.ToString(null,CultureInfo.InvariantCulture)??"",_=>value.ToString()??""};
    private static void ValidateArchive(byte[] bytes,CancellationToken ct)
    {
        using var stream=new MemoryStream(bytes,false);using var archive=new ZipArchive(stream,ZipArchiveMode.Read);
        if(archive.Entries.Count is <1 or >512||archive.GetEntry("xl/workbook.xml") is null)throw new ValidationException("XLSX structure is invalid.");
        long expanded=0;var buffer=new byte[8192];
        foreach(var entry in archive.Entries)
        {
            if(entry.FullName.Contains("vbaProject",StringComparison.OrdinalIgnoreCase)||entry.FullName.Contains("/externalLinks/",StringComparison.OrdinalIgnoreCase)
                ||entry.FullName.Contains("/embeddings/",StringComparison.OrdinalIgnoreCase))throw new ValidationException("Active/external spreadsheet content is unsupported.");
            if(entry.Length>16*1024*1024||entry.Length>Math.Max(1024*1024,entry.CompressedLength*200))throw new DomainException("Spreadsheet expansion exceeds its permitted size.",413);
            using var part=entry.Open();int count;
            while((count=part.Read(buffer,0,buffer.Length))!=0){ct.ThrowIfCancellationRequested();expanded+=count;if(expanded>32*1024*1024)throw new DomainException("Spreadsheet expansion exceeds 32 MiB.",413);}
        }
    }
}
