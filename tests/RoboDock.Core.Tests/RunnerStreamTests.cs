using System.Text;
using RoboDock.Core;

namespace RoboDock.Core.Tests;

/// <summary>
/// 针对"真实管道行为"的回归测试。
/// 这两条用例都是从实际界面里发现的 bug：中文乱码、进度百分比粘在文件名上。
/// </summary>
public class RunnerStreamTests
{
    static RunnerStreamTests()
    {
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
        catch { /* 忽略 */ }
    }

    /// <summary>按固定块大小返回数据的流，用来模拟子进程分块写管道。</summary>
    private sealed class ChunkedStream(byte[] data, int chunkSize) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var available = Math.Min(count, Math.Min(chunkSize, data.Length - _position));
            if (available <= 0) return 0;

            Array.Copy(data, _position, buffer, offset, available);
            _position += available;
            return available;
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static List<string> ReadAll(Stream stream)
    {
        var lines = new List<string>();
        var enumerator = RobocopyRunner.ReadLinesAsync(stream).GetAsyncEnumerator();
        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                lines.Add(enumerator.Current);
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        return lines;
    }

    [Fact]
    public void AsciiHeaderThenGbkPayloadIsDecodedWithoutMojibake()
    {
        // 复刻真实输出：开头是纯 ASCII 的分隔线，中文在后面
        var text =
            "-------------------------------------------------------------------------------\r\n" +
            "   ROBOCOPY     ::     Windows 的可靠文件复制\r\n" +
            "-------------------------------------------------------------------------------\r\n" +
            "\r\n" +
            "  开始时间: 2026年10月30日 2:01:43\r\n" +
            "\t    新文件    \t\t      10\tF:\\demo\\文件1.txt\r\n" +
            "\t    新文件    \t\t      10\tF:\\demo\\文件2.txt\r\n" +
            "\t    新文件    \t\t      10\tF:\\demo\\文件3.txt\r\n" +
            "       文件:         5         5         0         0         0         2\r\n";

        var bytes = Encoding.GetEncoding(936).GetBytes(text);

        // 每块只有 7 字节：第一块必定全是 ASCII，正是之前误判成 UTF-8 的场景
        var lines = ReadAll(new ChunkedStream(bytes, 7));

        Assert.Contains(lines, l => l.Contains("可靠文件复制"));
        Assert.Contains(lines, l => l.Contains("文件1.txt"));
        Assert.Contains(lines, l => l.Contains("文件:"));
        Assert.DoesNotContain(lines, l => l.Contains('\uFFFD'));
    }

    [Fact]
    public void CarriageReturnProgressDoesNotPolluteFileLines()
    {
        // robocopy 用 '\r' 原地刷新百分比，旧实现会把它粘到路径后面
        const string text = "\t    新文件    \t\t      10\tF:\\demo\\a.txt\r100%  \r\n下一行\r\n";

        var lines = ReadAll(new ChunkedStream(Encoding.UTF8.GetBytes(text), 5));

        Assert.Contains(lines, l => l.EndsWith("a.txt"));
        Assert.Contains(lines, l => l.Trim() == "100%");
        Assert.DoesNotContain(lines, l => l.Contains("a.txt100%"));
    }

    [Fact]
    public void FileLinesFromRealRobocopyFormatParseAfterCarriageReturnSplit()
    {
        const string text = "\t  *额外 文件 \t\t       7\tF:\\demo\\多余A.txt\r\n";

        var lines = ReadAll(new ChunkedStream(Encoding.GetEncoding(936).GetBytes(text), 6));
        var entries = lines.Select(PreviewParser.ParseLine).Where(e => e is not null).Select(e => e!).ToList();

        Assert.Equal(1, entries.Count);
        Assert.Equal(PreviewKind.DeleteFile, entries[0].Kind);
        Assert.Equal(@"F:\demo\多余A.txt", entries[0].Path);
    }
}
