namespace System.Net.Http
{
    public record struct StringUri(string StringValue = null, Uri UriValue = null)
    {
        public Uri UriValue { get; } = UriValue ?? CreateUri(StringValue);

        public static implicit operator StringUri(string value)
        {
            return new StringUri(StringValue: value);
        }

        public static implicit operator Uri(StringUri value)
        {
            return value.UriValue ?? CreateUri(value.StringValue);
        }

        public static implicit operator StringUri(Uri value) => new(UriValue: value);

        public string AsString()
        {
            return StringValue ?? UriValue?.ToString();
        }

        private static Uri? CreateUri(string? uri) =>
            string.IsNullOrEmpty(uri) ? null : new Uri(uri, UriKind.RelativeOrAbsolute);
    }
}
