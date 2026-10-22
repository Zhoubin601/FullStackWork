using System.Buffers.Binary;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Backup;

// Bounded header inspection; full image decoding remains the renderer's responsibility in P7.
public static class ImageResourceValidator
{
    public static void Validate(string path, CanonicalResource expected)
    {
        using var stream=File.OpenRead(path);Span<byte> header=stackalloc byte[30];var read=stream.Read(header);int width=0,height=0;string? media=null;
        if(read>=24&&header[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&header[12..16].SequenceEqual("IHDR"u8))
        {media="image/png";width=checked((int)BinaryPrimitives.ReadUInt32BigEndian(header[16..20]));height=checked((int)BinaryPrimitives.ReadUInt32BigEndian(header[20..24]));}
        else if(read>=10&&(header[..6].SequenceEqual("GIF87a"u8)||header[..6].SequenceEqual("GIF89a"u8)))
        {media="image/gif";width=BinaryPrimitives.ReadUInt16LittleEndian(header[6..8]);height=BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]);}
        else if(read>=25&&header[..4].SequenceEqual("RIFF"u8)&&header[8..12].SequenceEqual("WEBP"u8))
        {
            media="image/webp";
            if(read>=30&&header[12..16].SequenceEqual("VP8X"u8)){width=1+header[24]+(header[25]<<8)+(header[26]<<16);height=1+header[27]+(header[28]<<8)+(header[29]<<16);}
            else if(header[12..16].SequenceEqual("VP8L"u8)&&header[20]==0x2f){var bits=BinaryPrimitives.ReadUInt32LittleEndian(header[21..25]);width=1+(int)(bits&0x3fff);height=1+(int)((bits>>14)&0x3fff);}
            else if(read>=30&&header[12..16].SequenceEqual("VP8 "u8)&&header[23..26].SequenceEqual(new byte[]{0x9d,1,0x2a})){width=BinaryPrimitives.ReadUInt16LittleEndian(header[26..28])&0x3fff;height=BinaryPrimitives.ReadUInt16LittleEndian(header[28..30])&0x3fff;}
        }
        else if(read>=2&&header[0]==0xff&&header[1]==0xd8)
        {
            media="image/jpeg";stream.Position=2;var markers=0;
            while(stream.Position<Math.Min(stream.Length,1048576)&&++markers<=10000)
            {
                if(stream.ReadByte()!=0xff)break;int marker;do{marker=stream.ReadByte();}while(marker==0xff);
                if(marker<0||marker is 0xd9 or 0xda)break;if(marker is 0x01 or >=0xd0 and <=0xd7)continue;
                var high=stream.ReadByte();var low=stream.ReadByte();if(high<0||low<0)break;var length=(high<<8)|low;if(length<2||stream.Position+length-2>stream.Length)break;
                if(marker is >=0xc0 and <=0xc3 or >=0xc5 and <=0xc7 or >=0xc9 and <=0xcb or >=0xcd and <=0xcf)
                {if(length<8)break;stream.ReadByte();high=stream.ReadByte();low=stream.ReadByte();height=(high<<8)|low;high=stream.ReadByte();low=stream.ReadByte();width=(high<<8)|low;break;}
                stream.Position+=length-2;
            }
        }
        if(media!=expected.MediaType||width<=0||height<=0||width!=expected.PixelWidth||height!=expected.PixelHeight)throw new ArgumentException("InvalidFormat：图片头信息与资源清单不一致。");
    }
}
