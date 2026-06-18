using Parquet;
using Parquet.Data;
using Parquet.Schema;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// Parquet+zstd codec for whole-market daily OHLCV partitions.
/// </summary>
public static class ParquetDailyPartitionCodec
{
    private static readonly ParquetSchema Schema = new(
        new DataField<string>("ticker"),
        new DataField<int>("date"),
        new DataField<double>("open"),
        new DataField<double>("high"),
        new DataField<double>("low"),
        new DataField<double>("close"),
        new DataField<long>("volume"));

    public static async Task WriteAsync(string path, IReadOnlyList<DailyBar> bars, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var ordered = bars.OrderBy(b => b.Ticker).ThenBy(b => b.Date).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = File.Create(path);
        using var writer = await ParquetWriter.CreateAsync(Schema, stream, cancellationToken: cancellationToken);
        writer.CompressionMethod = CompressionMethod.Zstd;

        using var group = writer.CreateRowGroup();
        await group.WriteColumnAsync(new DataColumn(Schema.DataFields[0], ordered.Select(b => b.Ticker).ToArray()), cancellationToken);
        await group.WriteColumnAsync(new DataColumn(Schema.DataFields[1], ordered.Select(b => b.Date.DayNumber).ToArray()), cancellationToken);
        await group.WriteColumnAsync(new DataColumn(Schema.DataFields[2], ordered.Select(b => (double)b.Open).ToArray()), cancellationToken);
        await group.WriteColumnAsync(new DataColumn(Schema.DataFields[3], ordered.Select(b => (double)b.High).ToArray()), cancellationToken);
        await group.WriteColumnAsync(new DataColumn(Schema.DataFields[4], ordered.Select(b => (double)b.Low).ToArray()), cancellationToken);
        await group.WriteColumnAsync(new DataColumn(Schema.DataFields[5], ordered.Select(b => (double)b.Close).ToArray()), cancellationToken);
        await group.WriteColumnAsync(new DataColumn(Schema.DataFields[6], ordered.Select(b => b.Volume).ToArray()), cancellationToken);
    }

    public static async Task<IReadOnlyList<DailyBar>> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        using var reader = await ParquetReader.CreateAsync(stream, cancellationToken: cancellationToken);
        using var group = reader.OpenRowGroupReader(0);

        var ticker = (string[])(await group.ReadColumnAsync(Schema.DataFields[0], cancellationToken)).Data;
        var dayNumbers = (int[])(await group.ReadColumnAsync(Schema.DataFields[1], cancellationToken)).Data;
        var open = (double[])(await group.ReadColumnAsync(Schema.DataFields[2], cancellationToken)).Data;
        var high = (double[])(await group.ReadColumnAsync(Schema.DataFields[3], cancellationToken)).Data;
        var low = (double[])(await group.ReadColumnAsync(Schema.DataFields[4], cancellationToken)).Data;
        var close = (double[])(await group.ReadColumnAsync(Schema.DataFields[5], cancellationToken)).Data;
        var volume = (long[])(await group.ReadColumnAsync(Schema.DataFields[6], cancellationToken)).Data;

        var count = ticker.Length;
        var result = new List<DailyBar>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(new DailyBar
            {
                Ticker = ticker[i],
                Date = DateOnly.FromDayNumber(dayNumbers[i]),
                Open = (decimal)open[i],
                High = (decimal)high[i],
                Low = (decimal)low[i],
                Close = (decimal)close[i],
                Volume = volume[i]
            });
        }

        return result;
    }
}
