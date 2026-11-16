using System;
using System.Collections.Generic;
using Licensing.Client;

namespace Licensing.WinForms
{
    public static class LicensePresenter
    {
        public static LicenseFormState MachineUnavailable(string message)
        {
            return new LicenseFormState
            {
                StatusText = "读不到本机机器码",
                Guide = string.IsNullOrWhiteSpace(message) ? "这台电脑没有 MachineGuid，无法授权。" : message,
                ClockNote = "",
                MachineId = "",
                LicenseKey = "",
                Detail = "详细信息：MissingMachine",
                Tone = LicenseTone.Bad
            };
        }

        public static LicenseFormState NeedKey(string machineId)
        {
            var state = Base(machineId, "尚未授权", "请先填写许可码，或点「导入授权文件」。", LicenseTone.Normal);
            state.ShowActivate = true;
            state.ShowImport = true;
            state.Detail = "详细信息：NeedKey";
            return state;
        }

        public static LicenseFormState Failed(string machineId, string message)
        {
            var state = Base(machineId, "授权没有完成", message, LicenseTone.Bad);
            state.ShowActivate = true;
            state.ShowImport = true;
            state.Detail = "详细信息：Error";
            return state;
        }

        public static LicenseFormState Present(LicenseDecision decision, LicenseNames names, string machineId, string licenseKey, bool missedServer)
        {
            if (decision == null)
                return Failed(machineId, "没有得到授权结果。");

            names = names ?? new LicenseNames();
            var status = decision.Status ?? "";
            var single = string.IsNullOrEmpty(decision.Kind) || decision.Kind == "SingleMachine";
            var active = status == LicenseStatuses.Active && single;
            var state = Base(machineId, Headline(status, active), "", ToneFor(status, active));
            state.LicenseKey = licenseKey ?? "";
            state.KeyReadOnly = active;
            state.CanEnter = active;
            state.ClockNote = decision.ClockRollback ? "观察到系统时间比上次记录早。不影响本次使用。" : "";
            state.Detail = "详细信息：" + (status.Length == 0 ? "Unknown" : status);
            state.Edition = DisplayEdition(decision, names, active);
            state.Features = DisplayFeatures(decision, names, active);
            state.ExpiresText = FormatTerm(decision);

            if (status == LicenseStatuses.Active && !single)
            {
                state.StatusText = "不能使用这份授权";
                state.Guide = "本窗口只处理单机许可。当前这份是" + KindName(decision.Kind) + "。请联系供应商。";
                state.Tone = LicenseTone.Bad;
                state.CanEnter = false;
                state.KeyReadOnly = true;
                return state;
            }

            state.Guide = Guide(status, active, decision.FromCache, missedServer);
            ApplyButtons(state, status, active, decision.FromCache);
            return state;
        }

        static LicenseFormState Base(string machineId, string status, string guide, LicenseTone tone)
        {
            return new LicenseFormState
            {
                StatusText = status,
                Guide = guide ?? "",
                ClockNote = "",
                MachineId = machineId ?? "",
                LicenseKey = "",
                Edition = "",
                Features = "",
                ExpiresText = "",
                Detail = "",
                Tone = tone
            };
        }

        static void ApplyButtons(LicenseFormState state, string status, bool active, bool fromCache)
        {
            if (active)
                return;

            switch (status)
            {
                case LicenseStatuses.MachineInUse:
                    state.ShowImport = true;
                    state.ShowRehost = true;
                    state.RehostEnabled = true;
                    break;
                case LicenseStatuses.RehostLimit:
                    state.ShowImport = true;
                    state.ShowRehost = true;
                    state.RehostEnabled = false;
                    break;
                case LicenseStatuses.MachineMismatch:
                case LicenseStatuses.InvalidSignature:
                    state.ShowActivate = true;
                    state.ShowImport = true;
                    break;
                case LicenseStatuses.Suspended:
                    state.ShowRecheck = true;
                    break;
                case LicenseStatuses.Revoked:
                    break;
                case LicenseStatuses.Expired:
                    state.ShowRecheck = true;
                    state.ShowImport = fromCache;
                    break;
                default:
                    state.ShowActivate = true;
                    state.ShowImport = true;
                    break;
            }
        }

        static string Guide(string status, bool active, bool fromCache, bool missedServer)
        {
            if (active)
            {
                var text = "许可到期日是后台设定的日期，按 UTC 当天结束。离线可用至从最近一次成功联网起算宽限天数，默认 7 天，不会晚于许可到期日。再次联网检查成功后，离线可用至会往后延。到期前可以不上网。永久许可没有许可到期日，仍有离线可用至。";
                if (missedServer)
                    text += "\r\n刚才没连上授权服务器，在离线可用至之前可以继续使用。";
                return text;
            }

            switch (status)
            {
                case LicenseStatuses.NoGrant:
                    return "1. 这台电脑能上网：把许可码贴到下面，点「联网激活」。\r\n2. 不能上网：点「复制机器码」发给供应商。拿到授权文件后，点「导入授权文件」。";
                case LicenseStatuses.NotFound:
                    return "许可码不对，或不属于本程序。请核对后重新点「联网激活」。";
                case LicenseStatuses.MachineInUse:
                    return "这张许可已经在另一台电脑上。换到本机需要上网，并计入本年度自助换机次数。旧电脑上已经拷走的离线文件，在它自己的到期日前还能用。若提示已达到本年换机次数，请把机器码发给供应商，由后台处理。";
                case LicenseStatuses.RehostLimit:
                    return "今年的自助换机次数已用完。请把上面的机器码发给供应商，由他们在后台换机。";
                case LicenseStatuses.MachineMismatch:
                    return "这份授权不是这台电脑的。请把上面的机器码发给供应商重新生成，然后点「导入授权文件」。若这张许可码已在别的电脑上用过，也可以点「联网激活」，再按提示换到这台电脑。";
                case LicenseStatuses.InvalidSignature:
                    return "授权文件和本程序不匹配，或文件被改过。请向供应商重新索取，或改用「联网激活」。";
                case LicenseStatuses.Suspended:
                    return "请联系供应商。暂停恢复后，点「重新检查」。在此之前不要导入原来的授权文件。";
                case LicenseStatuses.Revoked:
                    return "请联系供应商。已吊销的许可不能继续使用，也不要导入原来的授权文件。";
                case LicenseStatuses.Expired:
                    return fromCache
                        ? "若能上网，点「重新检查」。若是离线文件到期，把机器码发给供应商再要一份文件，然后点「导入授权文件」。"
                        : "服务器确认这份许可已到期。请联系供应商。重新签发后，点「重新检查」。不要导入原来的文件。";
                case LicenseStatuses.Unreachable:
                    return "连不上授权服务器。可以点「复制机器码」，把机器码发给供应商。拿到授权文件后，点「导入授权文件」。网络恢复后也可以再点「联网激活」。";
                case LicenseStatuses.Released:
                    return "这台电脑上的授权已经解除。可以重新联网激活，或导入授权文件。";
                case LicenseStatuses.SeatLimit:
                    return "没有空余席位。请联系供应商。";
                case LicenseStatuses.ServerError:
                    return "授权服务器出错。请稍后再点「联网激活」，或改用「导入授权文件」。";
                case LicenseStatuses.InvalidRequest:
                    return "请先填写许可码，或点「导入授权文件」。";
                default:
                    return "许可检查没有通过。请核对许可码，或联系供应商。";
            }
        }

        static string FormatTerm(LicenseDecision decision)
        {
            var offline = decision.ExpiresAt == null
                ? ""
                : decision.ExpiresAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            string licenseEnd = null;
            if (decision.Perpetual)
                licenseEnd = "永久";
            else if (decision.SubscriptionEndsAt != null)
                licenseEnd = decision.SubscriptionEndsAt.Value.UtcDateTime.ToString("yyyy-MM-dd");

            if (licenseEnd == null)
                return offline;
            if (offline.Length == 0)
                return licenseEnd;
            return licenseEnd + "\r\n离线可用至 " + offline;
        }

        static string Headline(string status, bool active)
        {
            if (active)
                return "已授权";

            switch (status)
            {
                case LicenseStatuses.NoGrant:
                    return "尚未授权";
                case LicenseStatuses.NotFound:
                    return "没有这张许可";
                case LicenseStatuses.MachineInUse:
                    return "许可绑定在另一台机器上";
                case LicenseStatuses.RehostLimit:
                    return "已达到本年换机次数";
                case LicenseStatuses.MachineMismatch:
                    return "授权与这台电脑不一致";
                case LicenseStatuses.InvalidSignature:
                    return "许可签名无效";
                case LicenseStatuses.Expired:
                    return "许可已到期";
                case LicenseStatuses.Suspended:
                    return "许可已暂停";
                case LicenseStatuses.Revoked:
                    return "许可已吊销";
                case LicenseStatuses.Unreachable:
                    return "连不上授权服务器";
                case LicenseStatuses.Released:
                    return "授权已解除";
                case LicenseStatuses.SeatLimit:
                    return "没有空余席位";
                case LicenseStatuses.ServerError:
                    return "授权服务器出错";
                case LicenseStatuses.InvalidRequest:
                    return "尚未授权";
                default:
                    return "许可检查没有通过";
            }
        }

        static LicenseTone ToneFor(string status, bool active)
        {
            if (active)
                return LicenseTone.Ok;

            switch (status)
            {
                case LicenseStatuses.MachineInUse:
                case LicenseStatuses.RehostLimit:
                case LicenseStatuses.Unreachable:
                case LicenseStatuses.Expired:
                    return LicenseTone.Warn;
                case LicenseStatuses.NoGrant:
                case LicenseStatuses.InvalidRequest:
                case LicenseStatuses.Released:
                    return LicenseTone.Normal;
                default:
                    return LicenseTone.Bad;
            }
        }

        static string DisplayEdition(LicenseDecision decision, LicenseNames names, bool active)
        {
            var text = names.Edition(decision.Edition);
            if (text.Length == 0 && active)
                return "（无）";
            return text;
        }

        static string DisplayFeatures(LicenseDecision decision, LicenseNames names, bool active)
        {
            var text = names.Features(decision.Features ?? new List<string>());
            if (text.Length == 0 && active)
                return "无";
            return text;
        }

        static string KindName(string kind)
        {
            switch (kind)
            {
                case "MachinePool":
                    return "机器池";
                case "Floating":
                    return "浮动";
                case "Dongle":
                    return "加密锁";
                case "LegacyToken":
                    return "导入老码";
                default:
                    return string.IsNullOrWhiteSpace(kind) ? "其他种类" : kind;
            }
        }
    }
}
