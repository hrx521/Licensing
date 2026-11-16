using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Licensing.Client
{
    public sealed class LicenseClient : IDisposable
    {
        readonly HttpClient _http;
        readonly bool _ownsHttp;
        readonly string _baseUrl;
        readonly PublicKeyRing _keys;
        readonly string _language;
        readonly LocalLicenseStore _store;

        public LicenseClient(string serverBaseUrl, PublicKeyRing keys, string language = "zh-CN", HttpClient httpClient = null, string storeRoot = null)
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl))
                throw new ArgumentException("Server address is required.", nameof(serverBaseUrl));
            _baseUrl = serverBaseUrl.TrimEnd('/');
            _keys = keys ?? new PublicKeyRing();
            _language = string.IsNullOrWhiteSpace(language) ? "zh-CN" : language;
            _ownsHttp = httpClient == null;
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            _store = new LocalLicenseStore(storeRoot);
        }

        public Task<LicenseDecision> ActivateAsync(LicenseCallRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            return PostAsync("activate", request, cancellationToken);
        }

        public Task<LicenseDecision> HeartbeatAsync(LicenseCallRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            return PostAsync("heartbeat", request, cancellationToken);
        }

        public Task<LicenseDecision> DeactivateAsync(LicenseCallRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            return PostAsync("deactivate", request, cancellationToken);
        }

        public Task<LicenseDecision> RehostAsync(LicenseCallRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            return PostAsync("rehost", request, cancellationToken);
        }

        public LicenseDecision ImportOfflineFile(LicenseCallRequest request, string fileJson)
        {
            var rollback = ObserveClock(request == null ? null : request.ProductId);
            if (request == null || string.IsNullOrWhiteSpace(request.ProductId) || string.IsNullOrWhiteSpace(request.MachineId))
                return Local(LicenseStatuses.InvalidRequest, false, rollback, null);

            var state = _store.LoadState(request.ProductId);
            if (!string.IsNullOrEmpty(state.DenialStatus))
                return Local(state.DenialStatus, true, rollback, null);

            OfflineGrantFile file;
            try
            {
                file = System.Text.Json.JsonSerializer.Deserialize<OfflineGrantFile>(fileJson ?? "", Serializer.Options);
            }
            catch (Exception)
            {
                file = null;
            }

            if (file == null || string.IsNullOrEmpty(file.Canonical) || string.IsNullOrEmpty(file.Signature) || string.IsNullOrWhiteSpace(file.LicenseKey))
                return Local(LicenseStatuses.InvalidRequest, false, rollback, null);
            if (_keys == null || !_keys.Verify(file.Canonical, file.Signature))
                return Local(LicenseStatuses.InvalidSignature, false, rollback, null);

            GrantPayload payload;
            try
            {
                payload = GrantCanonical.Read(file.Canonical);
            }
            catch (Exception)
            {
                return Local(LicenseStatuses.InvalidSignature, false, rollback, null);
            }

            if (!string.Equals(payload.ProductId, request.ProductId, StringComparison.Ordinal))
                return Local(LicenseStatuses.NotFound, false, rollback, payload);
            if (!string.Equals(payload.MachineId, request.MachineId, StringComparison.Ordinal))
                return Local(LicenseStatuses.MachineMismatch, false, rollback, payload);

            var expires = GrantCanonical.ParseTimestamp(payload.ExpiresAt);
            if (DateTimeOffset.UtcNow >= expires)
                return Local(LicenseStatuses.Expired, false, rollback, payload);

            _store.SaveGrant(request.ProductId, new SignedGrant { Canonical = file.Canonical, Signature = file.Signature });
            state = _store.LoadState(request.ProductId);
            state.LicenseKey = file.LicenseKey.Trim();
            _store.SaveState(request.ProductId, state);
            return FromPayload(payload, false, rollback, payload.Edition, payload.Features, expires, null);
        }

        public LicenseDecision CheckOffline(LicenseCallRequest request)
        {
            var rollback = ObserveClock(request == null ? null : request.ProductId);
            if (request == null || string.IsNullOrWhiteSpace(request.ProductId))
                return Local(LicenseStatuses.InvalidRequest, false, rollback, null);

            var state = _store.LoadState(request.ProductId);
            if (!string.IsNullOrEmpty(state.DenialStatus))
                return Local(state.DenialStatus, true, rollback, null);

            var grant = _store.LoadGrant(request.ProductId);
            if (grant == null || string.IsNullOrEmpty(grant.Canonical))
                return AllowUnconfirmedDongle(request, rollback);

            if (_keys == null || !_keys.Verify(grant.Canonical, grant.Signature))
                return AllowUnconfirmedDongle(request, rollback, LicenseStatuses.InvalidSignature);

            GrantPayload payload;
            try
            {
                payload = GrantCanonical.Read(grant.Canonical);
            }
            catch (Exception)
            {
                return Local(LicenseStatuses.InvalidSignature, true, rollback, null);
            }

            if (!string.Equals(payload.ProductId, request.ProductId, StringComparison.Ordinal))
                return Local(LicenseStatuses.NotFound, true, rollback, payload);

            if (payload.OfflinePolicy == "Dongle" || payload.Kind == "Dongle")
                return DecideDongle(request, payload, rollback);

            var expires = GrantCanonical.ParseTimestamp(payload.ExpiresAt);
            if (DateTimeOffset.UtcNow >= expires)
                return Local(LicenseStatuses.Expired, true, rollback, payload);

            if (payload.Kind != "LegacyToken" && !string.Equals(payload.MachineId, request.MachineId, StringComparison.Ordinal))
                return Local(LicenseStatuses.MachineMismatch, true, rollback, payload);

            return FromPayload(payload, true, rollback, payload.Edition, payload.Features, expires, null);
        }

        public void Dispose()
        {
            if (_ownsHttp)
                _http.Dispose();
        }

        async Task<LicenseDecision> PostAsync(string action, LicenseCallRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ProductId) || string.IsNullOrWhiteSpace(request.MachineId))
                return Local(LicenseStatuses.InvalidRequest, false, false, null);

            request.ClockRollback = request.ClockRollback || ObserveClock(request.ProductId);
            var saved = _store.LoadGrant(request.ProductId);
            if (saved != null && !string.IsNullOrEmpty(saved.Canonical) && !string.IsNullOrEmpty(saved.Signature))
            {
                request.CurrentCanonical = saved.Canonical;
                request.CurrentSignature = saved.Signature;
            }
            if (string.IsNullOrWhiteSpace(request.LicenseKey))
            {
                var remembered = _store.LoadState(request.ProductId).LicenseKey;
                if (!string.IsNullOrWhiteSpace(remembered))
                    request.LicenseKey = remembered;
            }
            try
            {
                var json = Serializer.Options;
                var body = new StringContent(System.Text.Json.JsonSerializer.Serialize(request, json), Encoding.UTF8, "application/json");
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/api/v1/" + action) { Content = body };
                httpRequest.Headers.TryAddWithoutValidation("Accept-Language", ClientText.IsEnglish(_language) ? "en" : "zh-CN");
                var response = await _http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                LicenseCallResponse parsed = null;
                try
                {
                    parsed = System.Text.Json.JsonSerializer.Deserialize<LicenseCallResponse>(text, json);
                }
                catch (Exception)
                {
                    parsed = null;
                }

                if (parsed == null || string.IsNullOrEmpty(parsed.Status))
                    return Local(LicenseStatuses.ServerError, false, request.ClockRollback, null);

                return Apply(request, parsed);
            }
            catch (HttpRequestException)
            {
                return Local(LicenseStatuses.Unreachable, false, request.ClockRollback, null);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Local(LicenseStatuses.Unreachable, false, request.ClockRollback, null);
            }
        }

        LicenseDecision Apply(LicenseCallRequest request, LicenseCallResponse response)
        {
            var rollback = request.ClockRollback;
            if (response.Status == LicenseStatuses.Active && response.Grant != null)
            {
                if (!_keys.Verify(response.Grant.Canonical, response.Grant.Signature))
                    return Local(LicenseStatuses.InvalidSignature, false, rollback, null);

                var payload = GrantCanonical.Read(response.Grant.Canonical);
                if (KeepsExistingGrant(request, payload))
                    return CheckOffline(request);

                _store.SaveGrant(request.ProductId, response.Grant);
                ClearDenial(request.ProductId);
                RememberKey(request);
                if (payload.OfflinePolicy == "Dongle" || payload.Kind == "Dongle")
                    return DecideDongle(request, payload, rollback, fromCache: false, serverMessage: response.Message);
                return FromPayload(payload, false, rollback, payload.Edition, payload.Features, GrantCanonical.ParseTimestamp(payload.ExpiresAt), response.Message);
            }

            if (response.Status == LicenseStatuses.Released)
            {
                _store.DeleteGrant(request.ProductId);
                ClearDenial(request.ProductId);
                return Local(LicenseStatuses.Released, false, rollback, null, response.Message);
            }

            if (response.Status == LicenseStatuses.Revoked || response.Status == LicenseStatuses.Suspended || response.Status == LicenseStatuses.Expired)
            {
                _store.DeleteGrant(request.ProductId);
                var state = _store.LoadState(request.ProductId);
                state.DenialStatus = response.Status;
                state.DenialAt = response.ServerTime == default(DateTimeOffset) ? DateTimeOffset.UtcNow : response.ServerTime;
                _store.SaveState(request.ProductId, state);
                return Local(response.Status, false, rollback, null, response.Message);
            }

            return Local(response.Status, false, rollback, null, response.Message);
        }

        LicenseDecision DecideDongle(LicenseCallRequest request, GrantPayload payload, bool rollback, bool fromCache = true, string serverMessage = null)
        {
            if (request.Dongle == null || string.IsNullOrWhiteSpace(request.Dongle.DongleId))
                return Local(LicenseStatuses.DongleMissing, fromCache, rollback, payload, serverMessage);
            if (!string.Equals(request.Dongle.DongleId.Trim(), payload.DongleId, StringComparison.Ordinal))
                return Local(LicenseStatuses.DongleMismatch, fromCache, rollback, payload, serverMessage);

            return FromPayload(
                payload,
                fromCache,
                rollback,
                request.Dongle.Edition,
                request.Dongle.Features,
                null,
                serverMessage);
        }

        LicenseDecision AllowUnconfirmedDongle(LicenseCallRequest request, bool rollback, string failureStatus = null)
        {
            if (request.Dongle != null && !string.IsNullOrWhiteSpace(request.Dongle.DongleId))
            {
                return new LicenseDecision
                {
                    Status = LicenseStatuses.Active,
                    Message = ClientText.DongleUnconfirmed(_language),
                    FromCache = true,
                    Edition = request.Dongle.Edition,
                    Features = request.Dongle.Features ?? new List<string>(),
                    ClockRollback = rollback,
                    Kind = "Dongle"
                };
            }

            return Local(failureStatus ?? LicenseStatuses.NoGrant, true, rollback, null);
        }

        bool ObserveClock(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                return false;

            var state = _store.LoadState(productId);
            var now = DateTimeOffset.UtcNow;
            var rollback = state.LastSeenUtc.HasValue && now < state.LastSeenUtc.Value.AddMinutes(-5);
            if (!rollback && (state.LastSeenUtc == null || now > state.LastSeenUtc.Value))
            {
                state.LastSeenUtc = now;
                _store.SaveState(productId, state);
            }

            return rollback;
        }

        bool KeepsExistingGrant(LicenseCallRequest request, GrantPayload incoming)
        {
            var existing = _store.LoadGrant(request.ProductId);
            if (existing == null || !_keys.Verify(existing.Canonical, existing.Signature))
                return false;

            GrantPayload current;
            try
            {
                current = GrantCanonical.Read(existing.Canonical);
            }
            catch (Exception)
            {
                return false;
            }

            if (current == null || string.Equals(current.LicenseId, incoming.LicenseId, StringComparison.Ordinal))
                return false;

            var stored = KeyNormalize.Normalize(_store.LoadState(request.ProductId).LicenseKey ?? "");
            var sent = KeyNormalize.Normalize(request.LicenseKey ?? "");
            return stored.Length > 0 && sent == stored;
        }

        void RememberKey(LicenseCallRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LicenseKey))
                return;
            var state = _store.LoadState(request.ProductId);
            state.LicenseKey = request.LicenseKey.Trim();
            _store.SaveState(request.ProductId, state);
        }

        void ClearDenial(string productId)
        {
            var state = _store.LoadState(productId);
            if (string.IsNullOrEmpty(state.DenialStatus))
                return;
            state.DenialStatus = null;
            state.DenialAt = null;
            _store.SaveState(productId, state);
        }

        LicenseDecision Local(string status, bool fromCache, bool rollback, GrantPayload payload, string serverMessage = null)
        {
            return new LicenseDecision
            {
                Status = status,
                Message = string.IsNullOrWhiteSpace(serverMessage) ? ClientText.ForStatus(_language, status) : serverMessage,
                FromCache = fromCache,
                ClockRollback = rollback,
                Kind = payload == null ? null : payload.Kind,
                Edition = payload == null ? null : payload.Edition,
                Features = payload == null || payload.Features == null ? new List<string>() : payload.Features,
                ExpiresAt = payload == null || string.IsNullOrEmpty(payload.ExpiresAt) ? (DateTimeOffset?)null : GrantCanonical.ParseTimestamp(payload.ExpiresAt),
                SubscriptionEndsAt = payload == null || string.IsNullOrEmpty(payload.SubscriptionEndsAt) ? (DateTimeOffset?)null : GrantCanonical.ParseTimestamp(payload.SubscriptionEndsAt),
                Perpetual = payload != null && payload.Perpetual,
                HeartbeatSeconds = payload == null ? 0 : payload.HeartbeatSeconds
            };
        }

        LicenseDecision FromPayload(GrantPayload payload, bool fromCache, bool rollback, string edition, IReadOnlyList<string> features, DateTimeOffset? expires, string serverMessage)
        {
            return new LicenseDecision
            {
                Status = LicenseStatuses.Active,
                Message = string.IsNullOrWhiteSpace(serverMessage) ? ClientText.ForStatus(_language, LicenseStatuses.Active) : serverMessage,
                FromCache = fromCache,
                Edition = edition,
                Features = features ?? new List<string>(),
                ExpiresAt = expires,
                SubscriptionEndsAt = string.IsNullOrEmpty(payload.SubscriptionEndsAt) ? (DateTimeOffset?)null : GrantCanonical.ParseTimestamp(payload.SubscriptionEndsAt),
                Perpetual = payload.Perpetual,
                ClockRollback = rollback,
                Kind = payload.Kind,
                HeartbeatSeconds = payload.HeartbeatSeconds
            };
        }
    }
}
