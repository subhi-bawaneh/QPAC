# DIP — Digital Information Delivery Platform
## الخطة النهائية لـ Claude Code — v2 (.NET 8 CQRS Vertical Slice + React TS)

> هذا الملف هو المرجع الوحيد للمشروع. Claude Code يقرأه قبل أي مهمة.
> **v3 (2026-09-08):** `docs/refactor-plan.md` هو المواصفة الحالية، ويلغي الأقسام المذكورة في § 13 منه (§ 1، 3.2، 3.4، 4، 6، 8، 9 هنا). الأقسام الملغاة أُعيدت كتابتها في مكانها بهذا الملف؛ ما بقي بلا تغيير هو خرائط أعمدة الإكسل ومعادلاتها وقواعد المحرك وأرقام العيّنات. سجل التنفيذ في `docs/refactor-log.md`.
> القاعدة الذهبية: **كل رقم يخرجه النظام يجب أن يطابق الرقم الموجود في ملفات الإكسل في `samples/`**. لا تُعتبر أي ميزة منتهية بدون اختبار يثبت المطابقة.
> **ممنوع** كتابة أي secret (connection string, API key, JWT key) في أي ملف داخل المستودع. كلها متغيرات بيئة / User Secrets.

---

## 0. الفكرة في فقرة

المقاول (Nesma & Partners) يسلّم رسومات ووثائق هندسية لمشروع QPAC عبر Aconex. التخطيط يبدأ بملفات **TIDP** (ملف لكل تخصص، على Google Drive في `qpac/Qpac_1/...`)، تُدمج في **MIDP**. يُصدَّر سجل Aconex (History) ويُقارن مع MIDP لإنتاج **Tracker**، **Corporate Summary**، **Baseline Summary**، **Control Findings**. كل هذا اليوم في Excel ثقيل يعدّله عدة أشخاص. الهدف: نظام واحد فيه:

1. **مستكشف مجلدات** يحاكي Google Drive (الشجرة مخزّنة في قاعدة البيانات، Drive يُستخدم للمزامنة فقط).
2. **طبقتا بيانات**: **Draft (Staging)** للاستيراد والمراجعة والتعديل، و**Live** للتقارير. كل مجلد له Target افتراضي Live، ويمكن تحويله إلى Draft، ومن Draft زر **Promote to Live** مع Diff.
3. محرك حسابي يعيد إنتاج Tracker والملخصات والـ Findings + SPI.
4. مصادقة ASP.NET Identity بأدوار وصلاحيات.

---

## 1. القرارات التقنية (ثابتة)

| البند | القرار |
|---|---|
| Backend | .NET 8 LTS, C# 12, ASP.NET Core **Web API with Controllers**, `nullable enable`, warnings as errors |
| البنية | **Vertical Slice / Feature-based** + CQRS بـ dispatcher مخصص (بدون MediatR) |
| قاعدة البيانات | **PostgreSQL على Neon** عبر EF Core 8 (`Npgsql.EntityFrameworkCore.PostgreSQL`). Migrations بـ EF. اتصال من `ConnectionStrings__Default` (env var). محلياً: نفس Neon أو Postgres في Docker |
| Excel | ClosedXML (MIT) |
| Google Drive | Google Drive API v3 عبر HTTP (`HttpClient`) بـ **API key** (`GoogleDrive__ApiKey` env var) — يتطلب مشاركة المجلد Anyone-with-link. خلف `IDriveClient` للتبديل لاحقاً إلى Service Account |
| Auth | ASP.NET Core Identity (EF stores في نفس القاعدة) + **JWT Bearer** (access 15 min + refresh token 7 days). Roles + Permission claims |
| Validation | FluentValidation في pipeline الـ dispatcher |
| Logging | Serilog (console + file). في ASPMonster: file في `App_Data/logs` |
| API docs | Swashbuckle (Swagger) على `/swagger` في Development فقط |
| Frontend | **React 18 + TypeScript + Vite**, feature-based, **shadcn/ui + Tailwind**, TanStack Query, TanStack Table, React Router, react-hook-form + zod, Recharts, axios |
| Hosting | API → **ASPMonster** (Windows/IIS/Plesk, .NET 8 in-process). Frontend → **Vercel**. CORS مضبوط على origin الـ Vercel |
| اختبارات | xUnit + FluentAssertions + Testcontainers (PostgreSQL) للتكامل؛ Vitest + Testing Library للواجهة |
| التوقيت | كل التواريخ `timestamp without time zone` (naive كما في الإكسل). Npgsql: `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` أو استخدام `DateTime` Unspecified — قرار واحد موثق في `docs/decisions/001-datetime.md`. الحفاظ على أجزاء الثانية في `DateModified` |

### قيود ASPMonster (استضافة مشتركة) — تؤثر على التصميم
> **v3 (2026-09-08):** المالك اختبر الاستضافة وأثبت أن `BackgroundService` و SignalR يعملان. راجع `docs/refactor-plan.md` § 5.4.
- العمل الطويل يجري داخل **عاملَين مستضافَين** في نفس عملية IIS: `DriveSyncWorker` (يستطلع Drive كل `GoogleDrive:PollHours`، افتراضياً 5) و `ImportWorker` (يستهلك طابور `WorkQueue`). التقدّم يُدفَع عبر **SignalR** على `/hubs/sync`. إعادة الحساب ما زالت **مجزّأة داخل العامل** حتى لا تمتد معاملة واحدة على 16 ألف صف.
- شرط تشغيلي: تجمّع التطبيقات يجب ألا يتوقف — `Start Mode = AlwaysRunning`، `Idle Time-out = 0`، `Preload Enabled = true` (انظر `docs/deploy.md` § 1.1). بدونها لا يعمل الاستطلاع.
- `requestTimeout` في `web.config` = 10 دقائق. حجم الرفع الأقصى **64 MB** (بايتات الملف تذهب إلى `FileBlobs` في القاعدة، لا إلى القرص).
- لا Docker على الاستضافة → النشر بـ `dotnet publish -c Release -r win-x64 --self-contained false` ورفع عبر Plesk/FTP. GitHub Action تُنتج artifact.
- متغيرات البيئة تُضبط من Plesk (Environment Variables) أو `web.config` `<environmentVariables>` (غير ملتزم — لا تضع secrets في web.config داخل المستودع).

---

## 2. بنية المستودع

```
dip/
├── backend/
│   ├── Dip.sln
│   ├── Directory.Build.props
│   ├── src/
│   │   ├── Dip.Domain/                    # Entities, Enums, ValueObjects, Domain events. لا اعتمادات
│   │   ├── Dip.Application/               # Abstractions فقط: ICommand/IQuery/Handlers, IDipDbContext, IDriveClient, IExcelReader, ICurrentUser, DTOs مشتركة, Engine (نقي)
│   │   │   ├── Abstractions/
│   │   │   ├── Behaviors/                 # Validation, Logging, Transaction, Authorization
│   │   │   └── Engine/                    # TrackerEngine, CorporateSummaryEngine, BaselineSummaryEngine, ControlFindingsEngine, EvmEngine
│   │   ├── Dip.Infrastructure/            # DipDbContext, Configurations, Migrations, Identity, Drive client, Excel readers/writers, Repositories, Time
│   │   │   ├── Persistence/
│   │   │   ├── Identity/
│   │   │   ├── Drive/
│   │   │   ├── Excel/
│   │   │   └── DependencyInjection.cs     # AddInfrastructure(config)
│   │   └── Dip.Api/                       # Controllers per feature, Program.cs, DependencyInjection.cs
│   │       ├── DependencyInjection.cs     # AddApi(): controllers, auth, swagger, cors, SignalR, dispatcher, behaviors, workers, all handlers by assembly scan
│   │       ├── Common/                    # ApiControllerBase, ProblemDetails, Pagination, Result, EffectiveDocumentLoader
│   │       ├── Workers/                   # WorkQueue, WorkerState, DriveSyncWorker, DriveSyncService, ImportWorker, FileImportService
│   │       ├── Hubs/                      # SyncHub (/hubs/sync), ISyncNotifier, HubSyncNotifier
│   │       └── Features/
│   │           ├── Auth/                  # Login, Refresh, Logout, Me
│   │           ├── Users/                 # CreateUser, AssignRole, ListUsers, GetUserById, SetDisciplines
│   │           ├── Projects/              # GetProject, UpdateSettings
│   │           ├── Folders/               # GetTree, GetFolder, CreateFolder, SetFolderTarget, SetFolderCompany, UploadFile, DownloadFile, GetFileWorkbook, TriggerDriveSync, GetDriveStatus
│   │           ├── Imports/               # GetImportStatus, ListImportBatches  (الاستيراد تلقائي — لا Start/Step)
│   │           ├── Drafts/                # ListDraftDocuments, UpdateDraftDocument, BulkUpdateDrafts, GetPromoteDiff, Promote, ConvertToLive
│   │           ├── Documents/             # ListDocuments(Midp), GetDocumentById, UpdateDocument (الطبقة الحية), CreateDocument, GenerateDocumentNumber
│   │           ├── Tidps/                 # ListTidps, GetTidp, ExportTidp
│   │           ├── Baseline/              # ListActivities, ImportBaseline, ExportBaseline
│   │           ├── Lists/                 # Picklists + StatusMappings CRUD
│   │           ├── Aconex/                # ImportHistory, ListRevisions, GetDocumentRevisions
│   │           ├── Tracker/               # GetTracker (paged), Recalculate, ExportTracker
│   │           ├── Summaries/             # GetCorporateSummary, GetBaselineSummary, GetEvm, GetDashboard
│   │           └── ControlFindings/       # GetFindings(kind), ExportFindings
│   └── tests/
│       ├── Dip.Domain.Tests/
│       ├── Dip.Engine.Tests/              # ضد أرقام samples/
│       ├── Dip.Infrastructure.Tests/      # Excel readers, Drive client (mocked HTTP)
│       └── Dip.Api.IntegrationTests/      # WebApplicationFactory + Testcontainers Postgres
├── frontend/                              # React TS (القسم 8)
├── samples/                               # TIDP-STL.xlsx, MIDP.xlsx, Tracker.xlsx
├── docs/
│   ├── excel-analysis.md
│   └── decisions/                         # ADRs قصيرة
├── .github/workflows/                     # backend-ci.yml, frontend-ci.yml, backend-publish.yml
├── CLAUDE.md
└── PLAN.md
```

### شكل الـ Feature الواحدة (إلزامي)

```
Features/Folders/SetFolderTarget/
├── SetFolderTargetCommand.cs        // public sealed record SetFolderTargetCommand(Guid FolderId, DataTarget Target) : ICommand<Unit>;
├── SetFolderTargetValidator.cs      // AbstractValidator<SetFolderTargetCommand>
├── SetFolderTargetHandler.cs        // ICommandHandler<SetFolderTargetCommand, Unit>
└── SetFolderTargetController.cs     // [ApiController][Route("api/folders/{id:guid}/target")][Authorize(Policy=Permissions.FoldersAssignTarget)] — يستدعي IDispatcher فقط

Features/Folders/GetFolder/
├── GetFolderQuery.cs                // IQuery<FolderDto>
├── GetFolderHandler.cs
├── FolderDto.cs
└── GetFolderController.cs
```

قواعد: Controller بلا منطق؛ Handler هو المنطق؛ Validator إلزامي لكل Command؛ Queries تستخدم `AsNoTracking` وتعيد DTO مباشرة (projection). لا Repository عام — الـ Handler يستخدم `IDipDbContext` مباشرة.

### الـ Dispatcher المخصص (Dip.Application/Abstractions)

```csharp
public interface ICommand<TResult> {}          public interface IQuery<TResult> {}
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult> { Task<TResult> Handle(TCommand c, CancellationToken ct); }
public interface IQueryHandler<TQuery, TResult>   where TQuery   : IQuery<TResult>   { Task<TResult> Handle(TQuery q, CancellationToken ct); }
public interface IDispatcher { Task<T> Send<T>(ICommand<T> c, CancellationToken ct); Task<T> Query<T>(IQuery<T> q, CancellationToken ct); }
// Pipeline behaviors (بالترتيب): Logging → Authorization(permission attribute على الـ command) → Validation → Transaction (للـ commands) → Handler
```

التسجيل: `Scrutor` scan لكل `ICommandHandler<,>`/`IQueryHandler<,>`/`IValidator<>` في assembly الـ Api.

---

## 3. نموذج البيانات

### 3.1 الهوية والصلاحيات

| الدور | الصلاحيات الافتراضية (claims `permission`) |
|---|---|
| **SuperAdmin** | كل الصلاحيات + `users.manage`, `roles.manage`, `system.settings` |
| **Admin** | `project.settings`, `folders.manage`, `folders.assignTarget`, `drive.sync`, `import.run`, `drafts.edit`, `drafts.promote`, `documents.editLive`, `reports.view`, `reports.export`, `lists.manage`, `baseline.manage` |
| **Manager** | `import.run`, `drafts.edit`, `drafts.promote`, `reports.view`, `reports.export`, `documents.editLive` |
| **Editor** | `drafts.edit` (مقيّد بـ claim `discipline:<Code>` واحد أو أكثر), `reports.view`, `reports.export` |
| **Viewer** | `reports.view`, `reports.export` |

الصلاحيات ثابتة في `Permissions` static class؛ الأدوار وخرائطها تُزرع بـ `IdentitySeeder` وقابلة للتعديل من `/admin/roles`. تقييد Editor بالتخصص يُطبَّق في Behavior الـ Authorization (يقارن `DisciplineCode` في الـ command مع claims المستخدم). مستخدم SuperAdmin الأول من env vars `Seed__AdminEmail` / `Seed__AdminPassword`.

كيانات: `ApplicationUser : IdentityUser<Guid>` (+ FullName, IsActive), `ApplicationRole : IdentityRole<Guid>`, `RefreshToken`.

### 3.2 المجلدات والملفات (مرآة Drive) — v3

```csharp
public class Folder { Guid Id; Guid ProjectId; Guid? ParentId; string Name; string Path /*"Qpac_1/TIDPs/Structural"*/;
    string? DriveFolderId; DataTarget Target = Live; int SortOrder; DateTime? LastSyncedAt; bool IsDeleted;
    bool IsCompany; Guid? AuthorId /*FK PicklistItem (Field = Author)*/; }
public enum DataTarget { Live, Draft }

public class FolderFile { Guid Id; Guid FolderId; string Name; FileKind Kind /*Tidp, Midp, Baseline, AconexHistory, Lists, Picklists, Unknown*/;
    string? DriveFileId; DateTime? DriveModifiedAt;                 // هوية Drive
    FileSource ContentSource /*Drive | Upload*/; DateTime ContentModifiedAt; string ContentMd5; long SizeBytes;
    ImportState State; string? ImportError; Guid? LastImportBatchId; DateTime? LastImportedAt; bool IsDeleted; }

public class FileBlob { Guid FolderFileId /*PK, FK cascade*/; byte[] Content; }   // bytea — لا تخزين على القرص
```

- `Kind` يُكتشف من اسم الملف (`-TDP-` → Tidp، `-MDP-` → Midp، `TRACKER`/`ACONEX` → AconexHistory، …). الاسم غير المعروف يُرفض عند الرفع (400) ويُعلَّم `Failed` عند المزامنة.
- **هوية الملف** = `(FolderId, lower(Name))` بين الصفوف غير المحذوفة (فهرس فريد مُرشَّح). المزامنة والرفع يحدّثان **نفس الصف**، والأحدث يفوز (`docs/refactor-plan.md` § 3 R2).
- **الشجرة الجذرية** = `GoogleDrive__RootFolderId`. `DriveSyncWorker` يسرد الأبناء تكرارياً، ينزّل الملف فقط إذا كان أحدث من المحفوظ **و** بصمة md5 مختلفة، ويحفظ البايتات في `FileBlobs`. ما اختفى من Drive يُعلَّم `IsDeleted` — لكن المرفوع يدوياً لا يُحذف أبداً.
- **الاستيراد تلقائي**: كل ملف جديد أو متغيّر يُدرَج في `WorkQueue` ويستورده `ImportWorker` (القرار D5).
- المستخدم يستطيع إنشاء مجلدات يدوية (بدون `DriveFolderId`) ورفع ملفات إليها. إنشاء مجلد **داخل** مجلد Drive مرفوض (400) لأنه سيختفي في الاستطلاع التالي. إعادة التسمية والحذف أُزيلت (القرار D6).
- **المجلد شركة** إذا `IsCompany = true`، ويحمل `AuthorId` من قائمة Author.

### 3.3 الطبقة الحية (Live)
`Project, Discipline, PicklistItem, BaselineActivity, Tidp, Document, DataExchange, AconexRevision, StatusMapping, ImportBatch, DocumentSnapshot, AuditLog` — تعريفها في القسم 5.2 أدناه. إضافات:
- `Document.FolderFileId` (من أي ملف جاء) و `Document.BudgetWeight` (decimal، افتراضياً = `DataExchange[0].DurationDays ?? 1`) لحساب SPI المرجّح.
- `Tidp.FolderFileId`.

### 3.4 طبقة المسودة (Draft)

نفس بنية Live لجداول التخطيط فقط (Aconex History و Baseline لا يحتاجان مسودة — يُستوردان دائماً Live مع إصدار Batch):

```csharp
public class TidpDraft     { ... نفس حقول Tidp ...; Guid FolderFileId; Guid ImportBatchId; DraftRowState State; }
public class DocumentDraft { ... نفس حقول Document ...; Guid TidpDraftId; Guid FolderFileId; Guid ImportBatchId;
    DraftRowState State /*New, Modified, Unchanged, Deleted, Conflict*/; Guid? LiveDocumentId; string? ConflictReason; bool IsDuplicate; }
public class DataExchangeDraft { ... }
public class PromoteBatch  { Guid Id; Guid FolderId; Guid FolderFileId; DateTime At; string By; int Added, Updated, Deleted, Skipped; }
```

**سلوك الاستيراد — v3 (`docs/refactor-plan.md` § 3 R3/R4/R5):**
- محتوى مصدره **Drive** (`ContentSource = Drive`) يُستورد دائماً إلى **Draft**، أياً كان Target المجلد. Drive لا يكتب الطبقة الحية أبداً.
- محتوى مصدره **رفع يدوي** يتبع `folder.Target`: Live → `Tidp/Document/DataExchange` مباشرة (upsert بـ DocumentNumber، AuditLog لكل حقل تغيّر)؛ Draft → `*Draft` مع حساب `State` لكل صف بمقارنته مع Live وتعليم `IsDuplicate` داخل الملف.
- سجل Aconex و Baseline و Picklists و Lists **دائماً Live**، حتى لو جاءت من مصنّف في مجلد Draft.

**Promote (Draft → Live)** لملف/مجلد:
1. `GetPromoteDiff` يعيد: Added, Modified (مع الحقول القديمة/الجديدة), Deleted (موجود Live من نفس الملف وغير موجود في المسودة — يُحذف فقط إذا اختار المستخدم `deleteMissing`), Conflicts (وثيقة موجودة Live من **ملف آخر** بنفس الرقم، أو Live عُدّل بعد تاريخ الاستيراد).
2. `Promote` ينفّذ داخل transaction واحدة، يكتب `PromoteBatch` و AuditLog، يعلّم Drafts `Unchanged`، ثم يُدرِج إعادة الحساب في الطابور.
3. **لا تراجع** (القرار D7): `AuditLog` هو أثر التراجع. بدلاً منه `ConvertToLive(folderId)` يرفع كل ملفات الشجرة ثم يقلب Target إلى Live، وهو قابل للتكرار — Drive يواصل تحديث المسودات ويظهر ذلك كـ `hasNewerDraft`.

---

## 4. Google Drive Client

```csharp
public interface IDriveClient {
    Task<IReadOnlyList<DriveEntry>> ListChildrenAsync(string folderId, CancellationToken ct); // files.list?q='{id}' in parents and trashed=false&fields=files(id,name,mimeType,modifiedTime,md5Checksum,size)&pageSize=1000&key=...
    Task<Stream> DownloadAsync(string fileId, CancellationToken ct);                          // files.get?alt=media&key=...
}
```
- `ApiKeyDriveClient` (افتراضي). خطأ 403/404 → رسالة واضحة "المجلد غير مشارك بوضع Anyone with the link".
- `ServiceAccountDriveClient` (لاحقاً، نفس الواجهة، `Google.Apis.Drive.v3`).
- **الواجهة قراءة فقط** ولن تُضاف إليها كتابة (القرار D1): عضوان لا ثالث لهما.
- **المزامنة عامل مستضاف** (`DriveSyncWorker` + `DriveSyncService`، `docs/refactor-plan.md` § 5.3): استعراض عرضي كامل للشجرة من الجذر، مرة كل `GoogleDrive:PollHours` وعند الطلب من `POST /api/projects/{id}/drive/sync` (يعيد 202). التقدّم على `/hubs/sync`.
- المزامنة لا تحذف ملفات مرفوعة يدوياً، ولا تستبدل بايتات أحدث منها.
- كل ملف جديد أو متغيّر يُستورد **تلقائياً** (القرار D5) — نص PLAN السابق «لا تستورد تلقائياً» ملغى.

---

## 5. الملفات المصدرية، الكيانات الحية، والمحرك الحسابي (مرجع دقيق)

### 5.1 الملفات المصدرية — وصف دقيق لكل شيت

#### 5.1.1 TIDP (مثال: `samples/TIDP-STL.xlsx`)

| الشيت | الاستخدام |
|---|---|
| `TIDP_Sheet` | الوثائق المخططة لتخصص واحد |
| `Baseline` | نسخة من جدول P6 (نفس شيت Baseline في Tracker) |
| `Picklists` | قوائم الترقيم (Numbering Scheme) |

**رأس الملف (Header block)** — الصفوف 3–11، العمود A = التسمية، العمود B = القيمة:
`CLIENT, PROJECT, ORGANISATION, DISCIPLINE, APPROVER, DATE CREATED, DATE LAST UPDATED, REVISION NUMBER, DOCUMENT REFERENCE`.
> لا تعتمد على رقم الصف. ابحث عن الخلية التي نصها `DISCIPLINE` في العمود A وخذ العمود B.

**جدول الوثائق**: الجدول المسمى `TIDP`. صف العناوين هو الصف الذي يحتوي `DOCUMENT NUMBER` في العمود A (الصف 16 في العينة). الأعمدة بالترتيب:

| # | عنوان الإكسل | الحقل في النظام | ملاحظات |
|---|---|---|---|
| A | DOCUMENT NUMBER | `DocumentNumber` | **معادلة** `CONCATENATE(TRIM(L),"-",TRIM(M),…,"-",TRIM(S)&TRIM(T)&TRIM(U))` — يُعاد توليده في النظام، لا يُقرأ |
| B | DOCUMENT TITLE | `Title` | |
| C | EXTRACTED FROM MODEL | `ExtractedFromModel` | |
| D | SCOPE AREA | `ScopeArea` | |
| E | AUTHORING SOFTWARE | `AuthoringSoftware` | |
| F | EXCHANGE FORMAT | `ExchangeFormat` | مثل `.dwg,.pdf` |
| G | SCALE | `Scale` | |
| H | DELIVERY MILESTONE | `DeliveryMilestone` (date) | يُستخدم في Working Plan |
| I | PACKAGE NAME | `PackageName` | غالباً فارغ |
| J | ACTIVITY ID | `ActivityId` | مفتاح الربط بـ Baseline |
| K | CLASSIFICATION CODE | `ClassificationCode` | Uniclass مثل `FI_60_25` |
| L | PROJECT | `Field01_Project` | `QF01012` |
| M | ORIGINATOR | `Field02_Originator` | `NES` |
| N | CONTRACT | `Field03_Contract` | `C04518` |
| O | DOCUMENT TYPE | `Field04_DocType` | `SDW`, `CAL`, `REP`… |
| P | DISCIPLINE | `Field05_Discipline` | `STL`, `STR`, `ARC`… |
| Q | AREA/ZONE | `Field06_Zone` | `00`, `01`… |
| R | VENUE/BUILDING | `Field07_Building` | `Z00000`, `BLAD03`, `CENT01`… |
| S | DRAWING TYPE | `Field08A_DrawingType` | رقم واحد 0–9 (قد يُقرأ كرقم — حوّله لنص) |
| T | LEVEL | `Field08B_Level` | `ZZ`, `B1`, `L2`… |
| U | SEQUENCE NUMBER | `Field08C_Sequence` | 4 أرقام نص مثل `0004` — **احفظ الأصفار** |
| V | CORPORATE DISCIPLINE | `CorporateDiscipline` | `Structural`, `Electrical`… (هذا ما تُبنى عليه التقارير) |
| W–AB | 01-AUTHOR, 01-GEOMETRICAL, 01-NON GEOMETRICAL, 01-DURATION (DAYS), 01-PREDECESSOR, 01-EXCHANGE DATE | `DataExchange[0]` | 01-EXCHANGE DATE معادلة = `XLOOKUP(ActivityId, Baseline.ActivityCode, Baseline.Finish)` |
| AC–AH | 02-… | `DataExchange[1]` | نفس البنية، Stage = HAND OVER |

قاعدة توليد رقم الوثيقة:
```
DocumentNumber = $"{F01}-{F02}-{F03}-{F04}-{F05}-{F06}-{F07}-{F08A}{F08B}{F08C}"
مثال: QF01012-NES-C04518-SDW-STL-00-BLAD03-2ZZ0207
```

#### 5.1.2 MIDP (`samples/MIDP.xlsx`)

| الشيت | الاستخدام |
|---|---|
| `MIDP` | نفس أعمدة TIDP بالضبط (A–AH) لكن لكل التخصصات، **15,885 وثيقة (Tracker يفلترها إلى 15,883)**. الحقل A قيمة لا معادلة. صف العناوين = الصف 15. **ملاحظة**: بلوك الرأس (rows 3–10) لا يحتوي DISCIPLINE، وAPPROVER في الصف 6 (مختلف عن TIDP). |
| `Aconex History` | تصدير Aconex الخام (**25,246 صفاً**) + أعمدة مساعدة (انظر 5.1.3). صف العناوين = **الصف 11**، Document No في العمود D. |
| `Aconex Latest` | نفس البنية، **5,752 صفاً** (Latest + Terminated). |
| `LISTS` | تحويل DC Status → Status (جدول **قديم**؛ **المرجع المعتمد هو شيت `Lists` في Tracker**). |

#### 5.1.3 Aconex History (في MIDP وفي Tracker باسم `SHD_History`)

صف العناوين = الصف الذي يحتوي `Document No` في العمود D (Tracker) أو C (MIDP). الأعمدة الخام من Aconex:

| عنوان | الحقل | ملاحظة |
|---|---|---|
| File | `FileType` | pdf/dwg |
| File Name | `FileName` | |
| Document No | `AconexDocNo` | **قد يحتوي مسافات بعد الشرطة** وقد ينتهي بـ `-PDF` — مثال `QF01012- NES- C04518- SDW- STR- 00- BLAD05- 2FL0101-PDF` |
| Revision | `Revision` | نص `00`, `01`… |
| Title | `Title` | |
| Status | `AconexStatus` | `A - Approved`, `Issued For Approval`, `Responded`… |
| Review Status | `ReviewStatus` | قد يكون `Terminated` |
| Date Modified | `DateModified` | datetime بأجزاء الثانية — **مفتاح** |
| Type | `Type` | Shop Drawing / Calculation / Report |
| Discipline | `Discipline` | |
| Area - Geographical / Zone | `Area` | |
| Venue - Building / Facilities | `Venue` | |
| Floor Level | `FloorLevel` | |
| Transmittal In | `TransmittalIn` | |

الأعمدة المساعدة (معادلات في الإكسل، **تُحسب في النظام** ولا تُقرأ):

| عمود | المعادلة | القاعدة في النظام |
|---|---|---|
| Document No Final | قيمة يدوية/XLOOKUP | `NormalizeDocNo(AconexDocNo)` = إزالة كل المسافات، إزالة لاحقة `-PDF` (case-insensitive). إذا لم تطابق نمط الترقيم (8 مقاطع مفصولة بـ `-` والمقطع الأخير 7 أحرف) → `"XXX"` |
| Document Length | `LEN(Document No Final)` | يُستخدم فقط لاستبعاد `XXX` (طول 3) |
| Terminated | `COUNTIFS(sameDoc, sameRev, ReviewStatus="Terminated", DateModified>=this) + COUNTIFS(..., Status="Closed", DateModified>=this) + COUNTIFS(..., Status="No Longer In Use", DateModified>=this) > 0` | لكل صف: يوجد صف آخر (أو هو نفسه) بنفس AconexDocNo ونفس Revision بتاريخ **≥** تاريخ هذا الصف وحالته Terminated/Closed/No Longer In Use |
| Latest | `MAXIFS(DateModified, DocNoFinal=this) = this.DateModified` | الصف هو الأحدث لهذه الوثيقة |
| In MIDP | `IF(Terminated, TRUE, COUNTIF(MIDP.DocumentNumber, DocNoFinal)>=1)` | الوثيقة موجودة في MIDP (أو ملغاة) |

> ملاحظة: في `Aconex Latest` عمود Document No Final يساوي `XXX` للوثائق التي لا تخص MIDP (تقارير، وثائق الاستشاري BSB…). طبّق نفس القاعدة.

#### 5.1.4 Baseline (شيت `Baseline` في Tracker و TIDP)

صف العناوين = الصف الذي يحتوي `Activity Code`. الأعمدة: `1..7` (مستويات WBS)، `Package` (= المستويات مدمجة بـ ` | `)، `Activity Code` (مثل `QP.E.ST.GEN.GEN.1000`)، `Activity` (**`Submittal` أو `Approval`**)، `Original Duration`، `Start`، `Finish`، `Used` (معادلة = هل يوجد وثيقة في MIDP بهذا Activity ID).

قاعدة مهمة: كل Package له نشاطان: Submittal (Activity Code ينتهي بـ …00/…70 مثلاً) و Approval. الربط بينهما بـ **Package** وليس بالكود.

#### 5.1.5 Tracker (`samples/Tracker.xlsx`) — المخرجات المطلوب إعادة إنتاجها

| الشيت | الوصف |
|---|---|
| `Tracker` | جدول MIDP + أعمدة محسوبة (القسم 5.4.1) |
| `SHD_History` / `SHD_Latest` | كما 5.1.3 |
| `Baseline` | كما 5.1.4 |
| `Corporate Summary` | 5.4.2 |
| `Baseline Summary` | 5.4.3 |
| `Control Findings` | 5.4.4 |
| `Lists` | جدول التحويل المعتمد (القسم 5.3) |

---

### 5.2 كيانات الطبقة الحية (Dip.Domain)

```csharp
// كل الكيانات تحمل ProjectId من اليوم الأول (تمهيداً لـ multi-tenant)

public class Project {
    public Guid Id; public string Code;          // "QF01012"
    public string Name;                          // "Qiddiya Performing Arts Center"
    public string Client; public string Organisation; public string Approver;
    public string? CostCenter;
    public ScheduleMode ScheduleMode;            // Baseline | WorkingPlan  (Tracker!AC8)
    public int WorkingPlanApprovalDays = 28;     // Planned Finish = Planned Start + 28 في وضع Working Plan
    public DateTime? ReportDate;                 // 'Corporate Summary'!C4 ; null => Today
    public DateTime BaselineStartDate = new(2025,11,4); // ثابت في المعادلة: DATE(2025,11,4)
}

public class Discipline { Guid Id; Guid ProjectId; string Code /*STL*/; string CorporateName /*Structural*/; }

public class PicklistItem {                      // شيت Picklists
    Guid Id; Guid ProjectId; PicklistField Field; // Project, Originator, Contract, DocType, Discipline, Zone, Building, DrawingType, Level, AuthoringSoftware, ExchangeFormat, ScopeArea, SuitabilityCode, Scale
    string Code; string Description; int SortOrder;
}

public class BaselineActivity {
    Guid Id; Guid ProjectId; string ActivityCode; string Package;
    string[] WbsLevels /*1..7*/; BaselineActivityType Type; // Submittal | Approval
    int OriginalDuration; DateTime Start; DateTime Finish;
}

public class Tidp {                              // ملف TIDP كوثيقة إدارية
    Guid Id; Guid ProjectId; Guid DisciplineId; string DocumentReference; string RevisionNumber;
    DateTime? DateCreated; DateTime? DateLastUpdated; string? SourceFileName;
}

public class Document {                          // صف واحد في TIDP/MIDP
    Guid Id; Guid ProjectId; Guid TidpId; Guid DisciplineId;
    string DocumentNumber;                       // UNIQUE (ProjectId, DocumentNumber)
    string Title; string? ExtractedFromModel; string? ScopeArea; string? AuthoringSoftware;
    string? ExchangeFormat; string? Scale; DateTime? DeliveryMilestone; string? PackageName;
    string? ActivityId; string? ClassificationCode;
    string F01Project, F02Originator, F03Contract, F04DocType, F05Discipline, F06Zone, F07Building, F08ADrawingType, F08BLevel, F08CSequence;
    string CorporateDiscipline;
    List<DataExchange> Exchanges;                // 01, 02
    // audit
    DateTime CreatedAt; string CreatedBy; DateTime UpdatedAt; string UpdatedBy; byte[] RowVersion;
}

public class DataExchange { Guid Id; Guid DocumentId; int Number /*1,2*/; string? Stage; string? ProgrammeRef;
    string? Author; string? Geometrical /*LOD400*/; string? NonGeometrical; int? DurationDays; string? Predecessor; DateTime? ExchangeDate; }

public class AconexRevision {                    // صف واحد من Aconex History
    Guid Id; Guid ProjectId; Guid ImportBatchId;
    string FileType; string FileName; string AconexDocNo; string DocNoFinal /*normalized or XXX*/;
    string Revision; string Title; string AconexStatus; string? ReviewStatus; DateTime DateModified;
    string? Type; string? Discipline; string? Area; string? Venue; string? FloorLevel; string? TransmittalIn;
    // محسوبة عند الاستيراد
    bool IsTerminated; bool IsLatest; bool InMidp;
    // UNIQUE (ProjectId, AconexDocNo, Revision, DateModified)
}

public class StatusMapping { Guid Id; Guid ProjectId; string AconexStatus; UnifiedStatus Status; } // القسم 3.4
public enum UnifiedStatus { Approved, Rejected, UnderReview, Withdrawn }

public class ImportBatch { Guid Id; Guid ProjectId; ImportKind Kind /*Tidp, Midp, AconexHistory, Baseline*/; string FileName; DateTime ImportedAt; string ImportedBy; int RowsRead; int RowsInserted; int RowsUpdated; int RowsSkipped; string? Log; }

public class DocumentSnapshot {                  // صف Tracker مُجسَّد — يُعاد بناؤه بعد كل استيراد (القسم 5.4.1)
    Guid DocumentId;                             // معرّف الصف المصدر: Document.Id (Live) أو DocumentDraft.Id (Draft) — بلا FK
    Guid ProjectId; DataTarget Layer; Guid? FolderFileId; DateTime ComputedAt;
    // أعمدة العرض منسوخة من الصف المصدر حتى لا يحتاج Tracker إلى أي join
    string DocumentNumber, Title, Type /*F04*/, Discipline /*CorporateDiscipline*/,
           Building /*F07*/, Level /*F08B*/, Trade /*F05*/; string? Author /*Exchange 1*/;
    DateTime? DeliveryMilestone; string? ActivityId; string? PackageName;
    int? SubmissionsCount; string? Revision; string? AconexStatus; UnifiedStatus? Status;
    DateTime? SubmissionDate; DateTime? DateModified; string? Transmittal;
    DateTime? PlannedStart; DateTime? PlannedFinish; DateTime? ActualStart; DateTime? ActualFinish;
}

public class AuditLog { Guid Id; Guid ProjectId; string EntityName; Guid EntityId; string Action; string? Field; string? OldValue; string? NewValue; string UserId; DateTime At; }
```

#### 5.3 جدول تحويل الحالة (يُزرع تلقائياً — Seed)

المصدر النهائي = `docs/excel-analysis.md § 6b` (يُدمج `Tracker.xlsx!Lists` + قيم Aconex الفعلية + `MIDP.xlsx!LISTS` القديم):

| Aconex / Source Status | Unified | ملاحظة |
|---|---|---|
| A - Approved | Approved | |
| B - Approved with Comments | Approved | **بصيغة الجمع** — Aconex الحديث |
| B - Approved with Comment | Approved | بصيغة المفرد — `Tracker!Lists` القديم |
| C - Revise and Resubmit | Rejected | |
| D - Rejected | Rejected | |
| E - Review Not Required | Approved | |
| Issued For Approval | UnderReview | |
| Issued for Action | UnderReview | مُشاهَد في البيانات، غير موجود في Lists — أضِفه |
| Issued for information | Approved | |
| For Review | UnderReview | |
| No Longer In Use | Withdrawn | **بحرف I كبير** — Aconex الفعلي |
| No Longer in Use | Withdrawn | defensive |
| Under Review | UnderReview | |
| QA Rejected | Rejected | |
| QA Checked | UnderReview | |
| Terminated | Withdrawn | من `Review Status` فقط — يُستخدم كـ override |
| Open | UnderReview | مُشاهَد — أضِف |
| Responded | UnderReview | مُشاهَد — أضِف |
| Submitted to Site | UnderReview | من `MIDP.xlsx!LISTS` القديم |
| Site Rejected | Rejected | قديم |
| Submitted to Client | UnderReview | قديم |
| For Information | Approved | قديم |

**قاعدة المطابقة**: case-insensitive + Trim + دمج المسافات الداخلية إلى مسافة واحدة قبل المقارنة. حالة غير معروفة → `null` + تحذير في `ImportBatch.Log`.

---

### 5.4 المحرك الحسابي (Dip.Application/Engine) — ترجمة المعادلات حرفياً

كل الدوال تأخذ `IReadOnlyList<Document>`, `IReadOnlyList<AconexRevision>`, `IReadOnlyList<BaselineActivity>`, `Project` وتعيد نتائج نقية (pure functions). لا وصول لقاعدة البيانات داخل المحرك.

> **v3 — مجموعة الوثائق الفعّالة (`docs/refactor-plan.md` § 3 R8):** المدخل `Documents` لم يعد جدول Live وحده. `EffectiveDocumentLoader` يجمع: صفوف `Document` لملفات هدفها Live (وأي `Document` بلا `FolderFileId`)، مع صفوف `DocumentDraft` لملفات هدفها Draft (باستثناء `IsDuplicate`)، ثم يزيل التكرار حسب `DocumentNumber` — يفوز الصف صاحب الملف الأحدث (`ContentModifiedAt`)، وعند التساوي يفوز Live. هكذا تظهر الشركة التي ما زالت تعمل في Drive في التقارير كما تظهر الشركة التي تعمل في النظام.

#### 5.4.1 Tracker — لكل Document

المفتاح الأساسي للمطابقة: `DocNoFinal` في Aconex = `DocumentNumber` في MIDP. مجموعة الصفوف الخاصة بالوثيقة: `R = revisions.Where(r => r.DocNoFinal == doc.DocumentNumber)`.

```
DateModified     = R.Max(DateModified)                          // MAXIFS ; null إذا R فارغة
latestRow        = R.Single(r => r.DateModified == DateModified) // إن تعدد: خذ الأول
Revision         = latestRow.Revision
AconexStatus     = latestRow.IsTerminated ? "Terminated" : latestRow.AconexStatus
Status           = StatusMapping[AconexStatus]                    // Unified
SubmissionsCount = int.TryParse(Revision) ? value + 1 : null      // Rev "00" => 1
SubmissionDate   = R.Where(r => r.Revision == Revision).Min(DateModified)   // أول تاريخ لهذه المراجعة
Transmittal      = latestRow.TransmittalIn (فارغ إذا 0)
ActualStart      = R.Min(DateModified)                            // MINIFS
ActualFinish     = Status == Approved ? DateModified : null

// الجدول الزمني
if ScheduleMode == Baseline:
    sub  = baseline.FirstOrDefault(b => b.ActivityCode == doc.ActivityId)
    PlannedStart  = sub?.Finish
    PlannedFinish = baseline.FirstOrDefault(b => b.Package == sub.Package && b.Type == Approval)?.Finish
else (WorkingPlan):
    PlannedStart  = doc.DeliveryMilestone
    PlannedFinish = PlannedStart + WorkingPlanApprovalDays
```

"Selected Revision" (أعمدة Sel*) = نفس الحسابات لكن لمراجعة يختارها المستخدم (`Sel Rev`) بدل الأحدث — تُنفذ كـ endpoint/شاشة تفاصيل وثيقة تعرض كل المراجعات، لا كأعمدة.

#### 5.4.2 Corporate Summary

`ReportDate = project.ReportDate ?? Today`. الأسابيع تنتهي **الأحد 23:59:59** (المعادلة تدوّر لآخر يوم في الأسبوع؛ تحقق من عيّنة: الأسبوع 2 = 2025-11-02 → 2025-11-09).

**ملاحظة مهمة**: شيت `Corporate Summary` في Tracker يجمع بـ **المجموعتين معاً**:
1. **Disciplines** (9): Architectural, Electrical, Façade, Fire & Life Safety, Infrastructure, Interior Design, Landscape, Mechanical, Structural — من `Document.CorporateDiscipline`.
2. **Authors** (13): AFCO, ALUTEC, DOKA, FallProtec, JINGGONG, NAP PMO, Nesma & Partners, Nesma PMO, Provisional Sum, RAWABI, Sana Al-Jazerah, Subcontractor - Unassigned, TKE — من `Document.Exchanges[0].Author` (المرحلة الأولى فقط).

المحرك يعيد نفس البنية لكل تصنيف (Discipline أو Author). الواجهة تعرضهما في شاشتين أو تبويبين.

```
StartWeek = WeekEnd(Min(PlannedStart, ActualStart, 2025-11-04))
EndWeek   = WeekEnd(Max(PlannedStart, ActualFinish))
CurrentWeek = WeekEnd(ReportDate)

لكل أسبوع w (From = To - 7 days):
  WeeklyPlanned   = Count(PlannedStart  <= To) - Count(PlannedStart  <= From)
  WeeklySubmitted = Count(ActualStart   <= To) - Count(ActualStart   <= From)
  WeeklyApproved  = Count(ActualFinish  <= To) - Count(ActualFinish  <= From)
  (+ تراكمي لكل منها للرسم البياني S-curve)

لكل Discipline (CorporateDiscipline، مرتّب):
  Progress:  Total, Planned(PlannedStart<=CurrentWeek), Submitted(ActualStart<=CurrentWeek), Approved(ActualFinish<=CurrentWeek)
  Quality:   Approved, Rejected, UnderReview, Withdrawn  (حسب Unified Status)
             TotalRevisions = Sum(SubmissionsCount) - Withdrawn
             Quality = Approved / (TotalRevisions - UnderReview)      // null إذا القسمة على صفر
  PlannedValue (أوزان: Pending 0, Sub1 0.6, Sub2 0.9, Approved 1.0):
             Pending  = Count(PlannedStart > CurrentWeek || PlannedStart == null)
             Approved = Count(PlannedFinish <= CurrentWeek)
             Sub2     = 0
             Sub1     = Count(PlannedStart <= CurrentWeek) - Approved - Sub2
             Planned% = (Sub1*0.6 + Sub2*0.9 + Approved*1.0) / Total
  EarnedValue (نفس الأوزان):
             Pending  = Count(Status != Approved && SubmissionsCount == null)
             Sub1     = Count(Status != Approved && SubmissionsCount == 1)
             Sub2     = Count(Status != Approved && SubmissionsCount >= 2)
             Approved = Count(Status == Approved)
             Completed% = (Sub1*0.6 + Sub2*0.9 + Approved*1.0) / Total
```

الأوزان تُحفظ في `Project` كإعدادات قابلة للتعديل (افتراضي 0/0.6/0.9/1).

#### 5.4.3 Baseline Summary

```
لكل Discipline: Total, Submitted(ActualStart!=null), Approved(ActualFinish!=null),
                CRevise(AconexStatus=="C - Revise and Resubmit"), DRejected(AconexStatus=="D - Rejected"),
                UnderReview(Status==UnderReview)

لكل Package (Activity Code من نوع Submittal، فريد، مرتّب):
  docs = documents.Where(d => d.ActivityId == code)
  Total, Submitted, Approved, CRevise, DRejected, UnderReview (نفس التعريفات)
  PackageStatus = Total == 0      ? Unused
                : Submitted == Total ? Submitted
                : Submitted == 0    ? Pending
                : Partial
ملخص: عدد الـ Packages وعدد الرسومات لكل PackageStatus.
```

#### 5.4.4 Control Findings (أربعة تقارير)

1. **Aconex vs MIDP — مُسلَّم لكن غير مخطط**:
   `revisions.Where(r => r.DocNoFinal.Length != 3 && !r.InMidp && r.IsLatest)` → Document No, Revision, Title, Status, DateModified.
2. **Unplanned in MIDP**: `documents.Where(d => snapshot.PlannedStart == null)` → Type, Discipline, DocNo, Title, PlannedStart, Author (Author = Exchange01.Author).
3. **Unused Baseline Packages**: Packages (Submittal) التي `Total == 0` من 5.4.3.
4. **Duplicate Document No**: `documents.GroupBy(DocumentNumber).Where(g => g.Count() > 1)` → كل الصفوف المكررة. (في النظام يجب أن يمنع القيد UNIQUE هذا أصلاً، لكن التقرير يبقى للبيانات المستوردة قبل التنظيف — لذلك الاستيراد يسمح بالتكرار **بعلامة** `IsDuplicate` ولا يرفض الملف.)

كل تقرير له Endpoint وشاشة، ويمكن تصديره Excel.

---


## 6. الاستيراد (Dip.Infrastructure/Excel + Features/Imports)

مبادئ:
- اكتشاف صف العناوين بالبحث عن نص معروف، لا برقم صف.
- الحفاظ على الأصفار: `Revision` → `PadLeft(2,'0')`, `Sequence` → `PadLeft(4,'0')`, `Zone` → `PadLeft(2,'0')`.
- كل استيراد = `ImportBatch` (FolderFileId, Kind, Target, RowsRead/Inserted/Updated/Skipped, Warnings JSON).
- Idempotent.
- **تلقائي، لا مجزّأ من المتصفح** (v3): `FileImportService` يقرأ البايتات من `FileBlob` عبر `MemoryStream` — لا مسار ملف، لا `ImportStagingRow` — ويوجّه الشيتات حسب `FileKind` (`docs/refactor-plan.md` § 3 R5): TIDP → `TIDP_Sheet`؛ MIDP → `MIDP` + `Aconex History` إن وُجد؛ Tracker → `SHD_History` + `Baseline` + `Lists`؛ وهكذا. كل مستورد يتلقى `Stream`.
- بعد كل استيراد → `Recalculate` في الطابور، وإعادة بناء `DocumentSnapshot` فوق المجموعة الفعّالة.

| Importer | المصدر | مفتاح Upsert | ملاحظات |
|---|---|---|---|
| `PicklistImporter` | `Pick_Lists` | (Field, Code) | 17 قائمة، كلٌّ تُحدَّد بنص عنوانها في الصف 5 (`docs/refactor-plan.md` § 7). الأكواد المحذوفة ناعماً لا تعود بالاستيراد. |
| `BaselineImporter` | `Baseline` | ActivityCode | خيار Replace |
| `TidpImporter` | `TIDP_Sheet` | DocumentNumber (مُولَّد) | Discipline من الهيدر؛ يكتب Live أو Draft حسب Target |
| `MidpImporter` | `MIDP` | DocumentNumber | Discipline من CORPORATE DISCIPLINE؛ Live أو Draft |
| `AconexHistoryImporter` | `Aconex History` / `SHD_History` | (AconexDocNo, Revision, DateModified) | دائماً Live؛ يحسب DocNoFinal ثم IsTerminated/IsLatest/InMidp دفعة واحدة |
| `ListsImporter` | `Lists` | AconexStatus | StatusMapping |

**التصدير**: TIDP/MIDP بقالب `samples/` (نسخ الملف ومسح صفوف الجدول)، Tracker/Summaries/Findings كجداول.

---

## 7. EVM (SPI فقط الآن، CPI عند توفر الساعات)

```
لكل تخصص وللمشروع، بتاريخ التقرير R (كل Actual يُفلتر ≤ R):
  weight(d) = d.BudgetWeight
  PV = Σ weight × pvFactor(d)    حيث pvFactor: PlannedFinish ≤ R → 1 ; PlannedStart ≤ R → 0.6 ; else 0
  EV = Σ weight × evFactor(d)    حيث evFactor: Approved(ActualFinish ≤ R) → 1 ; Submissions ≥ 2 → 0.9 ; Submissions = 1 → 0.6 ; else 0
  BAC = Σ weight
  SPI = EV / PV ; SV = EV − PV ; PV%, EV% = /BAC
  CPI = EV / AC  → null ما لم يوجد ActualEffort (جدول اختياري: DisciplineId, PeriodEnd, Hours)
```
الأوزان في `Project.EvWeights` (افتراضي 0 / 0.6 / 0.9 / 1). الـ Dashboard يعرض SPI لكل تخصص بألوان (≥1 أخضر، 0.9–1 أصفر، <0.9 أحمر).

---

## 8. الواجهة (frontend/) — React TS feature-based

```
frontend/
├── src/
│   ├── app/                 # router.tsx, providers.tsx (QueryClient, Auth), layout/ (Sidebar, Topbar)
│   ├── shared/
│   │   ├── api/             # axiosClient (baseURL من VITE_API_URL, interceptors: bearer + refresh), generated types (openapi-typescript من swagger.json)
│   │   ├── auth/            # useAuth, RequireAuth, RequirePermission, tokenStorage (memory + httpOnly refresh cookie fallback)
│   │   ├── ui/              # shadcn components (button, table, dialog, sheet, tabs, badge, toast, tree...)
│   │   ├── realtime/        # syncHub (@microsoft/signalr), SyncHubProvider, useSyncEvent, invalidation map
│   │   └── lib/             # utils, date formatting, csv/xlsx download
│   └── features/
│       ├── auth/            # LoginPage
│       ├── explorer/        # ExplorerPage (/tidps): شجرة يسار + شبكة بلاطات يمين، ExplorerToolbar, TileGrid, FolderTile, FileTile,
│       │                    # ContextMenu, UploadDialog, ConvertToLiveDialog, CompanyDialog
│       ├── workbook/        # WorkbookPage (/files/:fileId): Grid افتراضي + FormulaBar + SheetTabs + StatusBar + محرر خلية
│       ├── drafts/          # DraftReviewPage (جدول قابل للتعديل inline), PromoteDialog (Diff: tabs Added/Modified/Deleted/Conflicts)
│       ├── midp/            # MidpPage (grid + filters + export)
│       ├── baseline/        # BaselinePage (activities table, import/replace, used/unused)
│       ├── lists/           # ListsPage: 18 تبويباً (17 قائمة + Status mapping) مع إضافة/تعديل/حذف ناعم/استعادة/ترتيب
│       ├── tracker/         # TrackerPage (server paging, filters, document detail drawer مع كل المراجعات)
│       ├── dashboard/       # DashboardPage (/): tabs Overview | Corporate | Baseline | Control Findings | EVM (Recharts)
│       └── admin/           # UsersPage, RolesPage, ProjectSettingsPage
├── .env.example             # VITE_API_URL=
└── vercel.json              # rewrites SPA
```

- **قائمة الجانب (Sidebar)**: Dashboard · TIDPs · MIDP · Baseline · Tracker · Lists · Users · Settings. كل عنصر مخفي إذا لم تتوفر الصلاحية، و Dashboard نفسه يتطلب `reports.view` لأنه صار يحوي كل التقارير (القرار D8).
- **صفحة TIDPs** (الأهم): مستكشف مثل Drive — شجرة يسار، شبكة بلاطات يمين (المجلدات أولاً ثم الملفات)، breadcrumb قابل للنقر، نقر مزدوج يفتح، أسهم لوحة المفاتيح، قائمة سياق بالزر الأيمن، وسحب وإفلات للرفع. شريط الأدوات: Upload · Sync now · Target · Company/Author · Convert to Live · New folder (خارج مجلدات Drive فقط) · شارة حالة Drive. البلاطة تدور أثناء الاستيراد وتتغيّر شارتها عند انتهائه — كله عبر `/hubs/sync`.
- **فتح ملف** يفتح جدول بيانات (`/files/:fileId`) بأعمدة A–AH كما في المصنّف، العمود A للقراءة فقط لأن الخادم يعيد تركيبه من L..U.
- الأنماط: shadcn افتراضي + Tailwind، وضع داكن/فاتح، الجداول بـ TanStack Table + virtualization للجداول الكبيرة.
- النشر Vercel: `VITE_API_URL=https://<aspmonster-domain>/api`.

---

## 9. مراحل التنفيذ — لا تنتقل لمرحلة قبل نجاح اختبارات السابقة

### المرحلة 0 — الفهم
**0.1**: «اقرأ الملفات الثلاثة في `samples/` (سكربت C# مؤقت بـ ClosedXML) واكتب `docs/excel-analysis.md`: لكل شيت الغرض، صف العناوين، الأعمدة، 3 صفوف عيّنة، وكل معادلة مترجمة إلى قاعدة. قارن مع القسمين 5 و 5.4 من PLAN.md وسجّل الفروق في "Discrepancies".» → أراجعه يدوياً.

### المرحلة 1 — الهيكل، القاعدة، الهوية
**1.1**: «أنشئ `backend/` بالمشاريع في القسم 2، الحزم (Npgsql EF, ClosedXML, FluentValidation, Scrutor, Serilog, Swashbuckle, Identity, JwtBearer, xUnit, FluentAssertions, Testcontainers), `Directory.Build.props`, `.editorconfig`, `.gitignore`, `Dip.Api/DependencyInjection.cs` و `Dip.Infrastructure/DependencyInjection.cs`. الـ dispatcher والـ behaviors. `dotnet build` بدون تحذيرات.»
**1.2**: «الكيانات (القسمان 3 و 5) + `DipDbContext` + Configurations (فهارس، UNIQUE، أنواع التاريخ) + Migration Init + Seeder (StatusMapping، أدوار وصلاحيات القسم 3.1، مشروع QPAC، SuperAdmin من env). اختبار تكامل: الـ migration تُطبَّق على Testcontainers Postgres.»
**1.3**: «Features/Auth: Login (JWT + refresh), Refresh, Logout, Me. Features/Users: CRUD + AssignRole + SetDisciplines. Authorization behavior بالصلاحيات. اختبارات تكامل: Viewer لا يستطيع `import.run`، Editor بـ `discipline:STL` لا يعدّل STR.»

### المرحلة 2 — المجلدات و Drive
**2.1** ~~(ملغاة بـ v3 — انظر R1)~~: `IDriveClient` + `ApiKeyDriveClient` باقيان. المزامنة صارت `DriveSyncWorker`/`DriveSyncService`، والرفع يكتب `FileBlob` لا `App_Data/files`، و `RenameFolder`/`DeleteFile`/`DeleteFolder` أُزيلت.

### المرحلة 3 — الاستيراد
**3.1**: Baseline + Picklists + Lists importers + اختبارات (`QP.M.GN.GEN.GEN.1400` Submittal ينتهي 2025-10-30).
**3.2**: TidpImporter (Live و Draft) + اختبار `samples/TIDP-STL.xlsx`: Structural، أول رقم `QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004`، الأصفار محفوظة، وفي وضع Draft كل الصفوف State=New على قاعدة فارغة.
**3.3**: MidpImporter + اختبار: عدد الوثائق = عدد صفوف الشيت (**15,885** — راجع `docs/excel-analysis.md`)، وتعليم المكررات `IsDuplicate`.
**3.4**: AconexHistoryImporter + اختبارات النرمَلة (`QF01012- NES- C04518- SDW- STR- 00- BLAD05- 2FL0101-PDF` → بدون مسافات وبدون `-PDF`؛ تقرير BSB → `XXX`)، وعينة 100 صف تطابق IsTerminated/IsLatest/InMidp المخزنة في `Tracker.xlsx`.
**3.5** ~~(ملغاة بـ v3 — انظر R1)~~: الاستيراد صار تلقائياً في `ImportWorker`؛ بقي `GetImportStatus` و `ListImportBatches` فقط، واختبار التكامل يرفع المصنّف وينتظر العامل.

### المرحلة 4 — المسودة والنشر
**4.1**: Features/Drafts: ListDraftDocuments (paged), UpdateDraftDocument (مع توليد الرقم والتحقق من التكرار), BulkUpdate.
**4.2**: GetPromoteDiff + Promote (+ `ConvertToLive` في v3) + اختبارات: (أ) استيراد Draft ثم Promote على قاعدة فارغة = Added لكل الصفوف؛ (ب) تعديل صف ثم Promote = Modified واحد مع AuditLog؛ (ج) تعارض مع ملف آخر → Conflict ولا يُكتب. البند (د) Rollback ~~ملغى بـ v3 (القرار D7)~~ — `AuditLog` هو أثر التراجع.

### المرحلة 5 — المحرك
**5.1**: TrackerEngine + اختبار 200 وثيقة عشوائية ضد شيت Tracker (فرق صفر، التواريخ < 1 ثانية).
**5.2**: CorporateSummaryEngine + اختبار (ReportDate 2026-08-30، القيم موثّقة في `docs/excel-analysis.md § 4.4`):
- **Electrical** Total/Planned/Submitted/Approved = 2998/252/858/624 ؛ Rejected 188 ؛ UnderReview 46 ؛ TotalRevisions 1054 ؛ Quality ≈ 0.6190 ؛ Planned% 0.0833 ؛ Completed% 0.2591
- **Façade** 1009/300/98/63 ؛ Quality 0.3481 ؛ Planned% 0.2727 ؛ Completed% 0.0856
- **Fire & LS** 751/55/264/105 ؛ UnderReview 25 ؛ Quality 0.3251
- **Infrastructure** 346/48/80/62 ؛ Planned% 0.1387
- **Mechanical** 1513/293/527/130 ؛ UnderReview 99 ؛ Quality 0.2218
- **Structural** 5277/3412/3334/2628 ؛ Planned% 0.6445 ؛ Completed% 0.5939
- **Architectural** 1955/133/370/222 ؛ Quality 0.3881
- **Interior Design**, **Landscape**: كل الأرقام 0 (بدون Baseline / Aconex) — التغطية = 0
- الإجماليات الكلية Total row 5: 15883 / 4493 / 5531 / 3834 ؛ Rejected 1056 ؛ UnderReview 641 ؛ Withdrawn 0 ؛ TotalRevisions 8086 ؛ Quality 0.5150
- التوقيت: Start Week 2025-11-02 23:59:59 ؛ End Week 2028-10-08 23:59:59 ؛ الأسبوع 1 (2025-10-26→2025-11-02) Planned 11 Submitted 0 ؛ الأسبوع 5 (2025-11-23→2025-11-30) Planned 12 Submitted 65 ؛ الأسبوع 7 (2025-12-07→2025-12-14) Planned 307 Submitted 112 Approved 4
- **Authors** (13 صفاً): AFCO 40/32/24/24، JINGGONG 1283/1219/968/453، Nesma & Partners 10114/2826/4010/3050 (تحقق كلها)
**5.3**: BaselineSummaryEngine + اختبار (`QP.C.PS.GEN.GEN.1810` Unused؛ `…1850` Total 100 Pending).
**5.4**: ControlFindingsEngine + اختبار (العناصر المذكورة في 5.4 + الأعداد تطابق الشيت).
**5.5**: EvmEngine + اختبار (بوزن 1: Structural SPI ≈ 0.92، Electrical ≈ 3.1).
**5.6**: RecalculationService (مجزّأ) + Features/Tracker, Summaries, ControlFindings endpoints + Export.

### المرحلة 6 — الواجهة
**6.1**: scaffold Vite React TS + Tailwind + shadcn + router + auth (login, refresh, guards) + layout + sidebar بالصلاحيات.
**6.2** ~~(أُعيدت كتابتها في R4/R5)~~: صفحة TIDPs صارت `features/explorer` بشبكة بلاطات وقوائم سياق وتقدّم فوري عبر SignalR، وفتح الملف صار `features/workbook`.
**6.3**: DraftReview + PromoteDialog (Diff) + Rollback.
**6.4**: MIDP, Baseline, Lists, Tracker (+ document drawer).
**6.5** ~~(أُعيدت كتابتها في R4)~~: Summary و Control Findings اندمجتا في `DashboardPage` على `/` بخمسة تبويبات (القرار D8).
**6.6**: Admin (Users, Roles, Settings). Vitest للمكونات الحرجة (FolderTree, PromoteDialog).

### المرحلة 7 — النشر
**7.1**: `web.config` للـ IIS in-process، `backend-publish.yml` ينتج zip، دليل نشر ASPMonster في `docs/deploy.md` (env vars في Plesk، شهادة، CORS).
**7.2**: Vercel (`vercel.json`, env) + `frontend-ci.yml`.
**7.3**: Health endpoint `/health` (DB + Drive reachability)، Serilog files، rate limiting على Auth.

### v3 (2026-09-08) — `docs/refactor-plan.md`
R1 نواة الخادم (المخطط، لا قرص، Drive قراءة فقط، العمّال، الاستيراد التلقائي) · R2 الأهداف والشركات والتحويل والمجموعة الفعّالة و Tracker المُجسَّد وتعديل Live · R3 خلفية القوائم · R4 المستكشف والزمن الفعلي و Dashboard · R5 عارض المصنّف · R6 صفحة القوائم · R7 التوثيق. التفاصيل والانحرافات في `docs/refactor-log.md`.

### لاحقاً
Clash importer (Navisworks/ACC CSV) + Model health importer + ActualEffort (CPI) + Service Account Drive + Aconex API.

---

## 10. قواعد للعمل مع Claude Code

1. قبل تنفيذ أي Prompt: اقرأ `PLAN.md` و`docs/excel-analysis.md`، اعرض قائمة الملفات التي ستُنشأ/تُعدَّل، وانتظر الموافقة.
2. بعد كل Prompt: `dotnet build` بلا تحذيرات، `dotnet test` أخضر، (للواجهة `npm run lint && npm run typecheck && npm test`)، ثم commit برسالة تبدأ برقم المرحلة (`[5.2] Corporate summary engine`).
3. الأرقام المتوقعة في الاختبارات تُقرأ من `samples/` (cached values) حيثما أمكن.
4. عند التعارض بين PLAN.md والإكسل: الإكسل هو الصحيح؛ سجّل الفرق واقترح تعديل PLAN.md.
5. لا secrets في المستودع. `appsettings.json` يحوي مفاتيح فارغة فقط؛ القيم من env: `ConnectionStrings__Default`, `Jwt__Key`, `GoogleDrive__ApiKey`, `GoogleDrive__RootFolderId`, `Seed__AdminEmail`, `Seed__AdminPassword`, `Cors__Origins`.
6. لا مكتبة جديدة دون ذكر السبب.
7. الأداء: استيراد 25 ألف revision ومحرك كامل < 60 ثانية داخل العامل المستضاف. `AsNoTracking`, `AddRange` كل 2000 صف، قواميس بدل بحث خطي، `ExecuteUpdate/ExecuteDelete` حيث يناسب.
8. لا كود UI قبل مرور اختبار المحرك/الـ endpoint المقابل.
9. كل endpoint موثق في Swagger مع مثال؛ أنواع الواجهة تُولَّد من `swagger.json` (`npm run gen:api`).

---

## 11. أوامر التشغيل

```bash
# backend
cd backend
dotnet user-secrets set "ConnectionStrings:Default" "<neon-connection-string>" --project src/Dip.Api
dotnet user-secrets set "GoogleDrive:ApiKey" "<key>" --project src/Dip.Api
dotnet user-secrets set "GoogleDrive:RootFolderId" "<Qpac_1 folder id>" --project src/Dip.Api
# بديل محلي بدون مفتاح Google (Development فقط): يخدم مجلد Qpac_1 المحلي كأنه Drive
dotnet user-secrets set "GoogleDrive:LocalMirrorPath" "/home/<you>/docs/QPAC/Qpac_1" --project src/Dip.Api
dotnet user-secrets set "Jwt:Key" "<random 64 chars>" --project src/Dip.Api
dotnet ef migrations add Init -p src/Dip.Infrastructure -s src/Dip.Api
dotnet ef database update      -p src/Dip.Infrastructure -s src/Dip.Api
dotnet run --project src/Dip.Api          # https://localhost:5001/swagger
dotnet test

# frontend
cd frontend
cp .env.example .env    # VITE_API_URL=https://localhost:5001/api
npm i && npm run dev
npm run gen:api         # openapi-typescript from backend swagger
```
