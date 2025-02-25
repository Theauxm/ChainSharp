using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ChainSharp.Logging.Postgres.Utils;

public class UtcValueConverter()
    : ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
