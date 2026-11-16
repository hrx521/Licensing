using System;
using System.Threading.Tasks;
using Licensing.Client;

namespace Licensing.WinForms
{
    public sealed class SingleMachineLicense : IDisposable
    {
        readonly LicenseClient _client;
        readonly LocalLicenseStore _store;
        readonly string _productId;
        readonly string _appVersion;
        readonly LicenseNames _names;
        readonly string _machineError;

        public SingleMachineLicense(string serverBaseUrl, string productId, PublicKeyRing keys, LicenseNames names = null, string storeRoot = null, string appVersion = null)
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl))
                throw new ArgumentException("需要授权服务器地址。", "serverBaseUrl");
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentException("需要产品标识。", "productId");
            if (keys == null)
                throw new ArgumentNullException("keys");

            _productId = productId.Trim();
            _appVersion = appVersion;
            _names = names ?? new LicenseNames();
            _store = new LocalLicenseStore(storeRoot);
            _client = new LicenseClient(serverBaseUrl, keys, "zh-CN", null, storeRoot);
            try
            {
                MachineId = WindowsMachineId.Read();
            }
            catch (Exception ex)
            {
                _machineError = ex.Message;
                MachineId = "";
            }
        }

        public string MachineId { get; private set; }

        public string ProductId
        {
            get { return _productId; }
        }

        public LicenseFormState CheckOffline()
        {
            if (_machineError != null)
                return LicensePresenter.MachineUnavailable(_machineError);

            return Present(_client.CheckOffline(Request(null)), null, false);
        }

        public Task<LicenseFormState> ActivateAsync(string licenseKey)
        {
            if (_machineError != null)
                return Task.FromResult(LicensePresenter.MachineUnavailable(_machineError));

            var key = (licenseKey ?? "").Trim();
            if (key.Length == 0)
                return Task.FromResult(LicensePresenter.NeedKey(MachineId));

            return Finish(_client.ActivateAsync(Request(key)), key);
        }

        public LicenseFormState Import(string fileJson)
        {
            if (_machineError != null)
                return LicensePresenter.MachineUnavailable(_machineError);
            if (string.IsNullOrWhiteSpace(fileJson))
                return LicensePresenter.Failed(MachineId, "这个授权文件是空的。");

            return Present(_client.ImportOfflineFile(Request(null), fileJson), null, false);
        }

        public Task<LicenseFormState> RehostAsync(string licenseKey)
        {
            if (_machineError != null)
                return Task.FromResult(LicensePresenter.MachineUnavailable(_machineError));

            var key = FirstKey(licenseKey);
            if (key.Length == 0)
                return Task.FromResult(LicensePresenter.NeedKey(MachineId));

            return Finish(_client.RehostAsync(Request(key)), key);
        }

        public Task<LicenseFormState> RecheckAsync(string licenseKey)
        {
            if (_machineError != null)
                return Task.FromResult(LicensePresenter.MachineUnavailable(_machineError));

            var key = FirstKey(licenseKey);
            if (key.Length == 0)
                return Task.FromResult(LicensePresenter.NeedKey(MachineId));

            return Finish(_client.HeartbeatAsync(Request(key)), key);
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        async Task<LicenseFormState> Finish(Task<LicenseDecision> call, string typedKey)
        {
            LicenseDecision decision;
            try
            {
                decision = await call.ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                return LicensePresenter.Failed(MachineId, ex.Message);
            }

            if (decision != null && decision.Status == LicenseStatuses.Unreachable)
            {
                var offline = _client.CheckOffline(Request(typedKey));
                if (offline.Status == LicenseStatuses.Active && IsSingle(offline))
                    return Present(offline, typedKey, true);
                if (offline.Status == LicenseStatuses.Expired
                    || offline.Status == LicenseStatuses.Suspended
                    || offline.Status == LicenseStatuses.Revoked
                    || offline.Status == LicenseStatuses.MachineMismatch
                    || offline.Status == LicenseStatuses.InvalidSignature)
                    return Present(offline, typedKey, false);
            }

            return Present(decision, typedKey, false);
        }

        LicenseFormState Present(LicenseDecision decision, string typedKey, bool missedServer)
        {
            var key = typedKey ?? "";
            if (decision != null && decision.Status == LicenseStatuses.Active)
            {
                var remembered = RememberedKey();
                if (remembered.Length > 0)
                    key = remembered;
            }
            else if (key.Length == 0)
            {
                key = RememberedKey();
            }

            var readOnly = decision != null && decision.Status == LicenseStatuses.Active && IsSingle(decision);
            if (readOnly)
                key = DisplayKey(key);

            return LicensePresenter.Present(decision, _names, MachineId, key, missedServer);
        }

        LicenseCallRequest Request(string licenseKey)
        {
            return new LicenseCallRequest
            {
                ProductId = _productId,
                LicenseKey = string.IsNullOrWhiteSpace(licenseKey) ? null : licenseKey.Trim(),
                MachineId = MachineId,
                AppVersion = _appVersion
            };
        }

        string FirstKey(string typed)
        {
            var key = (typed ?? "").Trim();
            return key.Length == 0 ? RememberedKey() : key;
        }

        string RememberedKey()
        {
            var state = _store.LoadState(_productId);
            return state == null || string.IsNullOrWhiteSpace(state.LicenseKey) ? "" : state.LicenseKey.Trim();
        }

        static bool IsSingle(LicenseDecision decision)
        {
            return decision != null && (string.IsNullOrEmpty(decision.Kind) || decision.Kind == "SingleMachine");
        }

        static string DisplayKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";
            var grouped = KeyNormalize.FormatGrouped(KeyNormalize.Normalize(key));
            return string.IsNullOrEmpty(grouped) ? key.Trim() : grouped;
        }
    }
}
