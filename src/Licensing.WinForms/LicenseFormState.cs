namespace Licensing.WinForms
{
    public enum LicenseTone
    {
        Normal,
        Ok,
        Warn,
        Bad
    }

    public sealed class LicenseFormState
    {
        public string StatusText { get; set; }
        public string Guide { get; set; }
        public string ClockNote { get; set; }
        public string MachineId { get; set; }
        public string LicenseKey { get; set; }
        public bool KeyReadOnly { get; set; }
        public string Edition { get; set; }
        public string Features { get; set; }
        public string ExpiresText { get; set; }
        public string Detail { get; set; }
        public bool ShowActivate { get; set; }
        public bool ShowImport { get; set; }
        public bool ShowRehost { get; set; }
        public bool RehostEnabled { get; set; }
        public bool ShowRecheck { get; set; }
        public bool CanEnter { get; set; }
        public LicenseTone Tone { get; set; }
    }
}
