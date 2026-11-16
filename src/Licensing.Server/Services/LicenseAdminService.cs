using System.Security.Claims;
using System.Security.Cryptography;
using Licensing.Client;
using Licensing.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Licensing.Server.Services;

public sealed class LicenseAdminService
{
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    readonly IDbContextFactory<AppDbContext> _factory;
    readonly AuditService _audit;
    readonly ClientLicenseService _clients;

    public LicenseAdminService(IDbContextFactory<AppDbContext> factory, AuditService audit, ClientLicenseService clients)
    {
        _factory = factory;
        _audit = audit;
        _clients = clients;
    }

    public bool IsSuper(ClaimsPrincipal user) => user.HasClaim("super", "1");

    public async Task<HomeStats> StatsAsync(ClaimsPrincipal user)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var customers = await VisibleCustomers(db, user).CountAsync();
        var licenses = await VisibleLicenses(db, user).CountAsync(x => x.Status != LicenseState.Revoked);
        var holding = await VisibleActivations(db, user).CountAsync(x => x.ReleasedAt == null);
        var year = DateTimeOffset.UtcNow.Year;
        var licenseIds = VisibleLicenses(db, user).Select(x => x.Id);
        var rehosts = await db.RehostEvents.CountAsync(x => x.Year == year && x.Actor == RehostActor.Client && licenseIds.Contains(x.LicenseId));
        return new HomeStats(customers, licenses, holding, rehosts);
    }

    public async Task<List<CustomerRow>> CustomersAsync(ClaimsPrincipal user)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await VisibleCustomers(db, user)
            .OrderBy(x => x.Name)
            .Select(x => new CustomerRow(x.Id, x.Name, x.Email, x.Company, x.Phone, x.Notes, x.Licenses.Count))
            .ToListAsync();
    }

    public async Task<CustomerRow?> CustomerAsync(ClaimsPrincipal user, Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await VisibleCustomers(db, user)
            .Where(x => x.Id == id)
            .Select(x => new CustomerRow(x.Id, x.Name, x.Email, x.Company, x.Phone, x.Notes, x.Licenses.Count))
            .SingleOrDefaultAsync();
    }

    public async Task<string?> SaveCustomerAsync(ClaimsPrincipal user, Guid? id, string name, string? email, string? company, string? phone, string? notes)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0)
            return Ui.T("请填写名称。", "Name is required.");
        await using var db = await _factory.CreateDbContextAsync();
        Customer customer;
        var created = false;
        if (id == null)
        {
            customer = new Customer { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            db.Customers.Add(customer);
            created = true;
        }
        else
        {
            var found = await VisibleCustomers(db, user).SingleOrDefaultAsync(x => x.Id == id);
            if (found == null)
                return Ui.T("无权查看这个客户。", "You cannot view this customer.");
            customer = found;
        }

        customer.Name = name;
        customer.Email = Clean(email);
        customer.Company = Clean(company);
        customer.Phone = Clean(phone);
        customer.Notes = Clean(notes);
        if (created && !IsSuper(user))
            db.StaffCustomers.Add(new StaffCustomer { UserId = Actor(user)!, CustomerId = customer.Id });
        await db.SaveChangesAsync();
        await _audit.WriteAsync(Actor(user), created ? "CustomerCreate" : "CustomerUpdate", customer.Id, null, customer.Name);
        return null;
    }

    public async Task<List<LicenseRow>> LicensesForCustomerAsync(ClaimsPrincipal user, Guid customerId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (!await CanSeeAsync(db, user, customerId))
            return new();
        var licenses = await db.Licenses.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .Select(x => new LicenseRow(x.Id, x.KeyDisplay, x.Kind, x.Status, x.EditionCode, x.IsPerpetual, x.SubscriptionEndsAt, x.SeatLimit, x.DongleId, x.CreatedAt))
            .ToListAsync();
        return licenses.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<LicenseView?> LicenseAsync(ClaimsPrincipal user, Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var license = await db.Licenses.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Product)
            .Include(x => x.Features)
            .Include(x => x.Activations)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (license == null || !await CanSeeAsync(db, user, license.CustomerId))
            return null;
        var year = DateTimeOffset.UtcNow.Year;
        var used = await db.RehostEvents.CountAsync(x => x.LicenseId == id && x.Year == year && x.Actor == RehostActor.Client);
        return new LicenseView
        {
            Id = license.Id,
            CustomerId = license.CustomerId,
            CustomerName = license.Customer.Name,
            ProductName = license.Product.Name,
            PublicId = license.Product.PublicId,
            KeyDisplay = license.KeyDisplay,
            Kind = license.Kind,
            Status = license.Status,
            EditionCode = license.EditionCode,
            Features = license.Features.Select(x => x.FeatureCode).OrderBy(x => x).ToList(),
            SeatLimit = license.SeatLimit,
            PersonLabel = license.PersonLabel,
            IsPerpetual = license.IsPerpetual,
            SubscriptionEndsAt = license.SubscriptionEndsAt,
            GraceDays = Math.Max(1, license.GraceSeconds / 86400),
            HeartbeatHours = Math.Max(1, license.HeartbeatSeconds / 3600),
            ReleaseHours = Math.Max(1, license.FloatingReleaseSeconds / 3600),
            RehostLimitPerYear = license.RehostLimitPerYear,
            RehostsUsedThisYear = used,
            DongleId = license.DongleId,
            Activations = license.Activations.OrderByDescending(x => x.LastHeartbeatAt).Select(x => new ActivationRow(
                x.Id, x.MachineId, x.DongleId, x.LastHeartbeatAt, x.LastClockRollbackAt, x.AppVersion, x.ReleasedAt, x.ReleaseReason, x.FeatureMismatch, x.SnapshotEdition, x.SnapshotFeatures)).ToList()
        };
    }

    public async Task<(string? Error, string? Key)> IssueAsync(ClaimsPrincipal user, IssueInput input)
    {
        var error = ValidateIssue(input);
        if (error != null)
            return (error, null);
        await using var db = await _factory.CreateDbContextAsync();
        if (!await CanSeeAsync(db, user, input.CustomerId))
            return (Ui.T("无权为这个客户签发。", "You cannot issue for this customer."), null);
        var product = await db.Products.Include(x => x.Editions).Include(x => x.Features).SingleOrDefaultAsync(x => x.Id == input.ProductId);
        if (product == null)
            return (Ui.T("没有这个产品。", "Product was not found."), null);
        if (product.Editions.All(x => x.Code != input.EditionCode))
            return (Ui.T("版本不属于这个产品。", "Edition does not belong to this product."), null);
        var features = (input.FeatureCodes ?? new List<string>()).Distinct(StringComparer.Ordinal).ToList();
        if (features.Any(code => product.Features.All(x => x.Code != code)))
            return (Ui.T("功能代码不属于这个产品。", "A feature code does not belong to this product."), null);

        string display;
        string normalized;
        if (input.Kind == LicenseKind.LegacyToken)
        {
            display = (input.LegacyCode ?? "").Trim();
            normalized = KeyNormalize.Normalize(display);
            if (normalized.Length == 0)
                return (Ui.T("请填写老注册码。", "Enter the existing code."), null);
        }
        else
        {
            normalized = "";
            display = "";
            for (var attempt = 0; attempt < 5; attempt++)
            {
                display = NewKey();
                normalized = KeyNormalize.Normalize(display);
                if (!await db.Licenses.AnyAsync(x => x.KeyNormalized == normalized))
                    break;
            }
        }

        if (await db.Licenses.AnyAsync(x => x.KeyNormalized == normalized))
            return (Ui.T("这个许可码已经存在。", "This license key already exists."), null);

        var now = DateTimeOffset.UtcNow;
        var license = new License
        {
            Id = Guid.NewGuid(),
            CustomerId = input.CustomerId,
            ProductId = product.Id,
            KeyDisplay = display,
            KeyNormalized = normalized,
            Kind = input.Kind,
            EditionCode = input.EditionCode.Trim(),
            SeatLimit = input.Kind is LicenseKind.MachinePool or LicenseKind.Floating ? Math.Max(1, input.SeatLimit) : input.Kind == LicenseKind.SingleMachine ? 1 : 0,
            PersonLabel = Clean(input.PersonLabel),
            IsPerpetual = input.IsPerpetual,
            SubscriptionEndsAt = input.IsPerpetual ? null : input.SubscriptionEndsAt,
            GraceSeconds = Math.Max(1, input.GraceDays) * 86400,
            HeartbeatSeconds = Math.Max(1, input.HeartbeatHours) * 3600,
            FloatingReleaseSeconds = Math.Max(1, input.ReleaseHours) * 3600,
            RehostLimitPerYear = Math.Max(0, input.RehostLimitPerYear),
            Status = LicenseState.Active,
            DongleId = input.Kind == LicenseKind.Dongle ? Clean(input.DongleId) : null,
            CreatedAt = now,
            UpdatedAt = now,
            Features = features.Select(code => new LicenseFeature { FeatureCode = code }).ToList()
        };
        db.Licenses.Add(license);
        await db.SaveChangesAsync();
        await _audit.WriteAsync(Actor(user), "LicenseIssue", license.CustomerId, license.Id, license.KeyDisplay);
        return (null, license.KeyDisplay);
    }

    public async Task<string?> UpdateAsync(ClaimsPrincipal user, LicenseEdit edit)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var license = await db.Licenses.Include(x => x.Features).Include(x => x.Product).ThenInclude(x => x.Editions).Include(x => x.Product).ThenInclude(x => x.Features).SingleOrDefaultAsync(x => x.Id == edit.Id);
        if (license == null || !await CanSeeAsync(db, user, license.CustomerId))
            return Ui.T("无权修改这张许可。", "You cannot edit this license.");
        if (license.Status == LicenseState.Revoked)
            return Ui.T("已吊销的许可不能修改。", "A revoked license cannot be edited.");
        if (license.Product.Editions.All(x => x.Code != edit.EditionCode))
            return Ui.T("版本不属于这个产品。", "Edition does not belong to this product.");
        var features = (edit.FeatureCodes ?? new List<string>()).Distinct(StringComparer.Ordinal).ToList();
        if (features.Any(code => license.Product.Features.All(x => x.Code != code)))
            return Ui.T("功能代码不属于这个产品。", "A feature code does not belong to this product.");
        if (!edit.IsPerpetual && edit.SubscriptionEndsAt == null)
            return Ui.T("订阅许可需要到期日。", "A subscription needs an end date.");

        license.EditionCode = edit.EditionCode.Trim();
        db.LicenseFeatures.RemoveRange(license.Features.ToList());
        foreach (var code in features)
            db.LicenseFeatures.Add(new LicenseFeature { LicenseId = license.Id, FeatureCode = code });
        license.SeatLimit = license.Kind is LicenseKind.MachinePool or LicenseKind.Floating ? Math.Max(1, edit.SeatLimit) : license.SeatLimit;
        license.PersonLabel = Clean(edit.PersonLabel);
        license.IsPerpetual = edit.IsPerpetual;
        license.SubscriptionEndsAt = edit.IsPerpetual ? null : edit.SubscriptionEndsAt;
        license.GraceSeconds = Math.Max(1, edit.GraceDays) * 86400;
        license.HeartbeatSeconds = Math.Max(1, edit.HeartbeatHours) * 3600;
        license.FloatingReleaseSeconds = Math.Max(1, edit.ReleaseHours) * 3600;
        license.RehostLimitPerYear = Math.Max(0, edit.RehostLimitPerYear);
        if (license.Kind == LicenseKind.Dongle)
            license.DongleId = Clean(edit.DongleId);
        license.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await _audit.WriteAsync(Actor(user), "LicenseUpdate", license.CustomerId, license.Id, license.KeyDisplay);
        return null;
    }

    public async Task<string?> SetStatusAsync(ClaimsPrincipal user, Guid id, LicenseState status)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var license = await db.Licenses.Include(x => x.Activations).SingleOrDefaultAsync(x => x.Id == id);
        if (license == null || !await CanSeeAsync(db, user, license.CustomerId))
            return Ui.T("无权修改这张许可。", "You cannot edit this license.");
        if (license.Status == LicenseState.Revoked)
            return Ui.T("吊销不能恢复。", "A revoked license stays revoked.");
        if (status == LicenseState.Active && license.Status != LicenseState.Suspended)
            return Ui.T("只有暂停的许可可以恢复。", "Only a suspended license can be resumed.");
        var now = DateTimeOffset.UtcNow;
        license.Status = status;
        license.UpdatedAt = now;
        if (status != LicenseState.Active)
        {
            foreach (var row in license.Activations.Where(x => x.ReleasedAt == null))
            {
                row.ReleasedAt = now;
                row.ReleaseReason = status.ToString();
            }
        }
        await db.SaveChangesAsync();
        var action = status switch
        {
            LicenseState.Suspended => "LicenseSuspend",
            LicenseState.Active => "LicenseResume",
            _ => "LicenseRevoke"
        };
        await _audit.WriteAsync(Actor(user), action, license.CustomerId, license.Id, license.KeyDisplay);
        return null;
    }

    public async Task<string?> ForceReleaseAsync(ClaimsPrincipal user, Guid activationId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Activations.Include(x => x.License).SingleOrDefaultAsync(x => x.Id == activationId);
        if (row == null || !await CanSeeAsync(db, user, row.License.CustomerId))
            return Ui.T("无权释放。", "You cannot release this activation.");
        if (row.ReleasedAt == null)
        {
            row.ReleasedAt = DateTimeOffset.UtcNow;
            row.ReleaseReason = "Admin";
            await db.SaveChangesAsync();
        }
        await _audit.WriteAsync(Actor(user), "ForceRelease", row.License.CustomerId, row.LicenseId, row.MachineId);
        return null;
    }

    public async Task<string?> AdminMoveAsync(ClaimsPrincipal user, Guid licenseId, string machineId)
    {
        machineId = (machineId ?? "").Trim();
        if (machineId.Length == 0)
            return Ui.T("请填写机器码。", "Machine id is required.");
        await using var db = await _factory.CreateDbContextAsync();
        var license = await db.Licenses.Include(x => x.Activations).SingleOrDefaultAsync(x => x.Id == licenseId);
        if (license == null || !await CanSeeAsync(db, user, license.CustomerId))
            return Ui.T("无权换机。", "You cannot change the machine.");
        if (license.Kind is not (LicenseKind.SingleMachine or LicenseKind.MachinePool))
            return Ui.T("这种许可不按机器绑定。", "This license is not bound to a machine.");
        var now = DateTimeOffset.UtcNow;
        var from = license.Activations.Where(x => x.ReleasedAt == null && x.MachineId != machineId).Select(x => x.MachineId).FirstOrDefault();
        if (license.Kind == LicenseKind.SingleMachine)
        {
            foreach (var row in license.Activations.Where(x => x.ReleasedAt == null && x.MachineId != machineId))
            {
                row.ReleasedAt = now;
                row.ReleaseReason = "Admin";
            }
        }
        else
        {
            var holding = license.Activations.Where(x => x.ReleasedAt == null).ToList();
            var limit = Math.Max(1, license.SeatLimit);
            if (holding.All(x => x.MachineId != machineId) && holding.Count >= limit)
            {
                var oldest = holding.OrderBy(x => x.LastHeartbeatAt).First();
                oldest.ReleasedAt = now;
                oldest.ReleaseReason = "Admin";
                from = oldest.MachineId;
            }
        }

        var mine = license.Activations.Where(x => x.MachineId == machineId).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        if (mine == null)
        {
            mine = new Activation { Id = Guid.NewGuid(), LicenseId = license.Id, MachineId = machineId, CreatedAt = now };
            db.Activations.Add(mine);
        }
        mine.ReleasedAt = null;
        mine.ReleaseReason = null;
        mine.LastHeartbeatAt = now;
        db.RehostEvents.Add(new RehostEvent
        {
            Id = Guid.NewGuid(),
            LicenseId = license.Id,
            FromMachineId = from,
            ToMachineId = machineId,
            OccurredAt = now,
            Actor = RehostActor.Admin,
            Year = now.Year
        });
        await db.SaveChangesAsync();
        await _audit.WriteAsync(Actor(user), "AdminRehost", license.CustomerId, license.Id, machineId);
        return null;
    }

    public async Task<(string? Error, byte[]? Content, string? FileName)> PresignOfflineAsync(ClaimsPrincipal user, Guid licenseId, string machineId, DateOnly expiresOn)
    {
        machineId = (machineId ?? "").Trim();
        if (machineId.Length == 0)
            return (Ui.T("请填写机器码。", "Machine id is required."), null, null);
        if (machineId.Length > 200)
            return (Ui.T("机器码过长。", "Machine id is too long."), null, null);

        var now = DateTimeOffset.UtcNow;
        var exp = new DateTimeOffset(expiresOn.ToDateTime(new TimeOnly(23, 59, 59)), TimeSpan.Zero);
        if (exp <= now)
            return (Ui.T("到期日需要是今天或以后。", "The expiry date must be today or later."), null, null);

        await using var db = await _factory.CreateDbContextAsync();
        var license = await db.Licenses.Include(x => x.Product).Include(x => x.Features).Include(x => x.Activations).SingleOrDefaultAsync(x => x.Id == licenseId);
        if (license == null || !await CanSeeAsync(db, user, license.CustomerId))
            return (Ui.T("无权为这张许可预签。", "You cannot presign this license."), null, null);
        if (license.Kind != LicenseKind.SingleMachine)
            return (Ui.T("只有单机许可可以预签离线授权。", "Only a single-machine license can be presigned."), null, null);
        if (license.Status != LicenseState.Active)
            return (Ui.T("只有有效的许可可以预签。", "Only an active license can be presigned."), null, null);
        if (!license.IsPerpetual && (license.SubscriptionEndsAt == null || license.SubscriptionEndsAt <= now))
            return (Ui.T("许可已到期。", "License has expired."), null, null);
        if (!license.IsPerpetual && license.SubscriptionEndsAt != null && exp > license.SubscriptionEndsAt.Value)
            exp = license.SubscriptionEndsAt.Value;

        var others = license.Activations.Where(x => x.ReleasedAt == null && x.MachineId != machineId).ToList();
        var from = others.Select(x => x.MachineId).FirstOrDefault();
        foreach (var row in others)
        {
            row.ReleasedAt = now;
            row.ReleaseReason = "Admin";
        }

        var mine = license.Activations.Where(x => x.MachineId == machineId).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        if (mine == null)
        {
            mine = new Activation { LicenseId = license.Id, MachineId = machineId, CreatedAt = now };
            db.Activations.Add(mine);
        }
        mine.ReleasedAt = null;
        mine.ReleaseReason = null;
        mine.LastHeartbeatAt = now;
        if (from != null)
        {
            db.RehostEvents.Add(new RehostEvent
            {
                Id = Guid.NewGuid(),
                LicenseId = license.Id,
                FromMachineId = from,
                ToMachineId = machineId,
                OccurredAt = now,
                Actor = RehostActor.Admin,
                Year = now.Year
            });
        }

        var grant = _clients.SignGrant(license, machineId, TruncateSeconds(now), exp);
        var envelope = new OfflineGrantFile
        {
            Canonical = grant.Canonical,
            Signature = grant.Signature,
            LicenseKey = license.KeyDisplay
        };
        var json = System.Text.Json.JsonSerializer.Serialize(envelope, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        await db.SaveChangesAsync();
        await _audit.WriteAsync(Actor(user), "OfflinePresign", license.CustomerId, license.Id, machineId + " " + GrantCanonical.FormatTimestamp(exp));
        var name = license.Product.PublicId + "-offline.json";
        return (null, System.Text.Encoding.UTF8.GetBytes(json), name);
    }

    static DateTimeOffset TruncateSeconds(DateTimeOffset value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, TimeSpan.Zero);

    public async Task<List<ImportLineResult>> ImportAsync(ClaimsPrincipal user, Guid customerId, Guid productId, string editionCode, List<string> featureCodes, bool perpetual, DateTimeOffset? ends, int rehostLimit, string codes)
    {
        var results = new List<ImportLineResult>();
        await using var db = await _factory.CreateDbContextAsync();
        if (!await CanSeeAsync(db, user, customerId))
        {
            results.Add(new ImportLineResult("", false, Ui.T("无权导入到这个客户。", "You cannot import for this customer.")));
            return results;
        }
        var product = await db.Products.Include(x => x.Editions).Include(x => x.Features).SingleOrDefaultAsync(x => x.Id == productId);
        if (product == null || product.Editions.All(x => x.Code != editionCode))
        {
            results.Add(new ImportLineResult("", false, Ui.T("请选择产品和版本。", "Choose a product and an edition.")));
            return results;
        }
        var features = (featureCodes ?? new List<string>()).Distinct().ToList();
        if (features.Any(code => product.Features.All(x => x.Code != code)))
        {
            results.Add(new ImportLineResult("", false, Ui.T("功能代码不属于这个产品。", "A feature code does not belong to this product.")));
            return results;
        }
        if (!perpetual && ends == null)
        {
            results.Add(new ImportLineResult("", false, Ui.T("订阅许可需要到期日。", "A subscription needs an end date.")));
            return results;
        }
        var now = DateTimeOffset.UtcNow;
        foreach (var raw in (codes ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;
            var normalized = KeyNormalize.Normalize(line);
            if (await db.Licenses.AnyAsync(x => x.KeyNormalized == normalized))
            {
                results.Add(new ImportLineResult(line, false, Ui.T("已经存在。", "Already exists.")));
                continue;
            }
            var license = new License
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                ProductId = product.Id,
                KeyDisplay = line,
                KeyNormalized = normalized,
                Kind = LicenseKind.LegacyToken,
                EditionCode = editionCode,
                SeatLimit = 0,
                IsPerpetual = perpetual,
                SubscriptionEndsAt = perpetual ? null : ends,
                GraceSeconds = 7 * 86400,
                HeartbeatSeconds = 12 * 3600,
                FloatingReleaseSeconds = 12 * 3600,
                RehostLimitPerYear = Math.Max(0, rehostLimit),
                Status = LicenseState.Active,
                CreatedAt = now,
                UpdatedAt = now,
                Features = features.Select(code => new LicenseFeature { FeatureCode = code }).ToList()
            };
            db.Licenses.Add(license);
            await db.SaveChangesAsync();
            await _audit.WriteAsync(Actor(user), "Import", customerId, license.Id, line);
            results.Add(new ImportLineResult(line, true, Ui.T("已导入。", "Imported.")));
        }
        if (results.Count == 0)
            results.Add(new ImportLineResult("", false, Ui.T("没有可导入的行。", "There is nothing to import.")));
        return results;
    }

    public async Task<List<ProductDetail>> ProductsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var products = await db.Products.AsNoTracking().Include(x => x.Editions).Include(x => x.Features).OrderBy(x => x.Name).ToListAsync();
        return products.Select(x => new ProductDetail(
            x.Id,
            x.PublicId,
            x.Name,
            x.Editions.OrderBy(e => e.Code).Select(e => new CodeName(e.Code, e.Name)).ToList(),
            x.Features.OrderBy(f => f.Code).Select(f => new CodeName(f.Code, f.Name)).ToList())).ToList();
    }

    public async Task<string?> AddProductAsync(ClaimsPrincipal user, string publicId, string name)
    {
        publicId = (publicId ?? "").Trim();
        name = (name ?? "").Trim();
        if (!IsCode(publicId) || name.Length == 0)
            return Ui.T("产品标识只能包含字母、数字、点、下划线和横线。", "Product id may contain letters, digits, dots, underscores, and hyphens.");
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Products.AnyAsync(x => x.PublicId == publicId))
            return Ui.T("产品标识已存在。", "That product id already exists.");
        db.Products.Add(new Product { Id = Guid.NewGuid(), PublicId = publicId, Name = name });
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> AddEditionAsync(Guid productId, string code, string name)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (!IsCode(code) || name.Length == 0)
            return Ui.T("版本代码不合法。", "Edition code is invalid.");
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Editions.AnyAsync(x => x.ProductId == productId && x.Code == code))
            return Ui.T("版本代码已存在。", "That edition code already exists.");
        db.Editions.Add(new Edition { Id = Guid.NewGuid(), ProductId = productId, Code = code, Name = name });
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> AddFeatureAsync(Guid productId, string code, string name)
    {
        code = (code ?? "").Trim();
        name = (name ?? "").Trim();
        if (!IsCode(code) || name.Length == 0)
            return Ui.T("功能代码不合法。", "Feature code is invalid.");
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.FeatureDefinitions.AnyAsync(x => x.ProductId == productId && x.Code == code))
            return Ui.T("功能代码已存在。", "That feature code already exists.");
        db.FeatureDefinitions.Add(new FeatureDefinition { Id = Guid.NewGuid(), ProductId = productId, Code = code, Name = name });
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<List<AuditRow>> AuditAsync(ClaimsPrincipal user)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!IsSuper(user))
        {
            var ids = VisibleCustomers(db, user).Select(x => x.Id);
            query = query.Where(x => x.CustomerId != null && ids.Contains(x.CustomerId.Value));
        }
        var rows = (await query.ToListAsync()).OrderByDescending(x => x.At).Take(200).ToList();
        var actors = rows.Where(x => x.ActorUserId != null).Select(x => x.ActorUserId!).Distinct().ToList();
        var names = await db.Users.Where(x => actors.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Email ?? x.UserName ?? x.Id);
        return rows.Select(x => new AuditRow(x.At, x.ActorUserId != null && names.TryGetValue(x.ActorUserId, out var name) ? name : x.ActorUserId, x.Action, x.Detail, x.Ip)).ToList();
    }

    public async Task<List<CodeName>> SigningKeysAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var keys = await db.SigningKeys.AsNoTracking().ToListAsync();
        return keys.OrderByDescending(x => x.CreatedAt)
            .Select(x => new CodeName(x.Kid, x.IsCurrent ? "current" : "previous"))
            .ToList();
    }

    IQueryable<Customer> VisibleCustomers(AppDbContext db, ClaimsPrincipal user)
    {
        var query = db.Customers.AsQueryable();
        if (IsSuper(user))
            return query;
        var userId = Actor(user);
        var ids = db.StaffCustomers.Where(x => x.UserId == userId).Select(x => x.CustomerId);
        return query.Where(x => ids.Contains(x.Id));
    }

    IQueryable<License> VisibleLicenses(AppDbContext db, ClaimsPrincipal user)
        => db.Licenses.Where(x => VisibleCustomers(db, user).Select(c => c.Id).Contains(x.CustomerId));

    IQueryable<Activation> VisibleActivations(AppDbContext db, ClaimsPrincipal user)
        => db.Activations.Where(x => VisibleLicenses(db, user).Select(l => l.Id).Contains(x.LicenseId));

    async Task<bool> CanSeeAsync(AppDbContext db, ClaimsPrincipal user, Guid customerId)
        => await VisibleCustomers(db, user).AnyAsync(x => x.Id == customerId);

    static string? ValidateIssue(IssueInput input)
    {
        if (input.Kind == LicenseKind.Dongle && string.IsNullOrWhiteSpace(input.DongleId))
            return Ui.T("请填写锁号。", "Dongle id is required.");
        if (!input.IsPerpetual && input.SubscriptionEndsAt == null)
            return Ui.T("订阅许可需要到期日。", "A subscription needs an end date.");
        if (input.Kind is LicenseKind.MachinePool or LicenseKind.Floating && input.SeatLimit < 1)
            return Ui.T("席位数至少为 1。", "Seat count must be at least 1.");
        return null;
    }

    static string NewKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(25);
        var chars = new char[25];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return KeyNormalize.FormatGrouped(new string(chars));
    }

    static bool IsCode(string value)
        => value.Length is > 0 and <= 64 && value.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-');

    static string? Clean(string? value)
    {
        var text = (value ?? "").Trim();
        return text.Length == 0 ? null : text;
    }

    static string? Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);
}

public sealed record HomeStats(int Customers, int Licenses, int Activations, int RehostsThisYear);
public sealed record CustomerRow(Guid Id, string Name, string? Email, string? Company, string? Phone, string? Notes, int LicenseCount);
public sealed record LicenseRow(Guid Id, string KeyDisplay, LicenseKind Kind, LicenseState Status, string EditionCode, bool IsPerpetual, DateTimeOffset? SubscriptionEndsAt, int SeatLimit, string? DongleId, DateTimeOffset CreatedAt);
public sealed record ActivationRow(Guid Id, string MachineId, string? DongleId, DateTimeOffset LastHeartbeatAt, DateTimeOffset? LastClockRollbackAt, string? AppVersion, DateTimeOffset? ReleasedAt, string? ReleaseReason, bool FeatureMismatch, string? SnapshotEdition, string? SnapshotFeatures);
public sealed record CodeName(string Code, string Name);
public sealed record ProductDetail(Guid Id, string PublicId, string Name, List<CodeName> Editions, List<CodeName> Features);
public sealed record AuditRow(DateTimeOffset At, string? Actor, string Action, string? Detail, string? Ip);
public sealed record ImportLineResult(string Line, bool Ok, string Message);

public sealed class LicenseView
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string PublicId { get; set; } = "";
    public string KeyDisplay { get; set; } = "";
    public LicenseKind Kind { get; set; }
    public LicenseState Status { get; set; }
    public string EditionCode { get; set; } = "";
    public List<string> Features { get; set; } = new();
    public int SeatLimit { get; set; }
    public string? PersonLabel { get; set; }
    public bool IsPerpetual { get; set; }
    public DateTimeOffset? SubscriptionEndsAt { get; set; }
    public int GraceDays { get; set; }
    public int HeartbeatHours { get; set; }
    public int ReleaseHours { get; set; }
    public int RehostLimitPerYear { get; set; }
    public int RehostsUsedThisYear { get; set; }
    public string? DongleId { get; set; }
    public List<ActivationRow> Activations { get; set; } = new();
}

public sealed class IssueInput
{
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public LicenseKind Kind { get; set; }
    public string EditionCode { get; set; } = "";
    public List<string> FeatureCodes { get; set; } = new();
    public int SeatLimit { get; set; } = 1;
    public string? PersonLabel { get; set; }
    public bool IsPerpetual { get; set; } = true;
    public DateTimeOffset? SubscriptionEndsAt { get; set; }
    public int GraceDays { get; set; } = 7;
    public int HeartbeatHours { get; set; } = 12;
    public int ReleaseHours { get; set; } = 12;
    public int RehostLimitPerYear { get; set; }
    public string? DongleId { get; set; }
    public string? LegacyCode { get; set; }
}

public sealed class LicenseEdit
{
    public Guid Id { get; set; }
    public string EditionCode { get; set; } = "";
    public List<string> FeatureCodes { get; set; } = new();
    public int SeatLimit { get; set; }
    public string? PersonLabel { get; set; }
    public bool IsPerpetual { get; set; }
    public DateTimeOffset? SubscriptionEndsAt { get; set; }
    public int GraceDays { get; set; }
    public int HeartbeatHours { get; set; }
    public int ReleaseHours { get; set; }
    public int RehostLimitPerYear { get; set; }
    public string? DongleId { get; set; }
}
