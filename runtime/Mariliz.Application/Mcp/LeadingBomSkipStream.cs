namespace Mariliz.Application.Mcp;

// Cliente no Windows (o PowerShell, por exemplo) abre a entrada com o BOM do UTF-8. O SDK não reconhece a primeira mensagem
// com ele grudado e não responde; o servidor anterior aceitava (ADR-0009). Só o começo da entrada: é quando o BOM aparece.
internal sealed class LeadingBomSkipStream(Stream inner) : Stream
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];
    private byte[] _pending = [];
    private bool _checked;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_checked)
        {
            _checked = true;
            var head = new byte[Bom.Length];
            var read = 0;
            while (read < head.Length)
            {
                var count = await inner.ReadAsync(head.AsMemory(read), cancellationToken);
                if (count == 0) break;
                read += count;
                // Entrada que já não começa como o BOM não precisa esperar os três bytes.
                if (!head.AsSpan(0, read).SequenceEqual(Bom.AsSpan(0, read))) break;
            }
            _pending = read == Bom.Length && head.AsSpan().SequenceEqual(Bom) ? [] : head[..read];
        }
        if (_pending.Length > 0)
        {
            var count = Math.Min(_pending.Length, buffer.Length);
            _pending.AsSpan(0, count).CopyTo(buffer.Span);
            _pending = _pending[count..];
            return count;
        }
        return await inner.ReadAsync(buffer, cancellationToken);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
