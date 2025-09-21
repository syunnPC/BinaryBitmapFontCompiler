using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

internal class Logger
{
    public enum Level
    {
        Debug, Info, Warning, Error, Fatal
    }

    private static ConsoleColor ToConsoleColor(Level level) => level switch
    {
        Level.Debug => ConsoleColor.Gray,
        Level.Info => ConsoleColor.Blue,
        Level.Warning => ConsoleColor.Yellow,
        Level.Error => ConsoleColor.Red,
        Level.Fatal => ConsoleColor.Magenta,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    public static void Log(Level logLevel, string message, [CallerMemberName] string caller = "")
    {
        DateTime dt = DateTime.Now;
        string dts = dt.ToString("yyyy-MM-dd HH:mm:ss:fff");
        Console.Write($"{dts} {caller} [");
        Console.ForegroundColor = ToConsoleColor(logLevel);
        Console.Write(logLevel.ToString());
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"] {message}");
    }
}

internal class Program
{
    public static void Main(string[] args)
    {
        if(args.Length == 0)
        {
            Console.WriteLine($"Usage:\r\n\t{AppDomain.CurrentDomain.FriendlyName} <project file>");
            return;
        }

        if(!File.Exists(args[0]))
        {
            Console.WriteLine($"File {args[0]} not found. Usage:\r\n\t{AppDomain.CurrentDomain.FriendlyName} <project file>");
            return;
        }

        Console.WriteLine("Bitmap Font File Compiler v1.0");

        Dictionary<string, string> parseValueTree = new();
        string fontfilename;

        using(StreamReader sr = new(args[0]))
        {
            string? line;
            int c = 0;
            while((line = sr.ReadLine())!= null)
            {
                c++;
                if(line.StartsWith("//") || line.Length == 0)
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if(eq == -1)
                {
                    Logger.Log(Logger.Level.Warning, $"Unpared key:{line} at line {c}");
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value;
                if (key[key.Length - 1] == '=')
                {
                    value = "";
                }
                else
                {
                    value = line.Substring(eq + 1, line.Length - eq - 1).Trim();
                }

                if(parseValueTree.TryGetValue(key, out string? _))
                {
                    Logger.Log(Logger.Level.Error, $"Duplicated Key detected at line {c}, key {key} is already declared. New value will be ignored.");
                    continue;
                }

                parseValueTree.Add(key, value);
                Logger.Log(Logger.Level.Debug, $"Found key-value pair at line {c}:Key {key}, Value {value}");
            }
        }

        if(!parseValueTree.TryGetValue("FONT_FILE", out fontfilename))
        {
            Logger.Log(Logger.Level.Warning, $"FONT_FILE is not set. Trying {Path.GetFileNameWithoutExtension(args[0]) + ".rbf"}");
        }
        if(!File.Exists(fontfilename))
        {
            Logger.Log(Logger.Level.Fatal, $"Font file {fontfilename} not found. Exiting.");
            Console.Write("\nPress any key to exit...");
            Console.ReadKey(true);
            return;
        }
        Logger.Log(Logger.Level.Info, $"FONT_FILE:{fontfilename}");

        string filename = Path.GetFileNameWithoutExtension(args[0]) + ".bff";

        if (File.Exists(filename))
        {
            Logger.Log(Logger.Level.Warning, $"File {filename} already exists. The file will be overwritten.");
        }

        using (FileStream fs = new(filename, FileMode.Create))
        using (BinaryWriter writer = new(fs))
        {
            /*マジック 0xBF 0xFF を書き込み（BE）*/
            writer.Write((byte)0xBF);
            writer.Write((byte)0xFF);
            /* ファイルフォーマットバージョン 1 */
            writer.Write((byte)1);
            /* 文字セット */
            if (!parseValueTree.TryGetValue("CHARSET", out string charset))
            {
                Logger.Log(Logger.Level.Fatal, $"Font parameter CHARSET not found. Exiting.");
                Console.Write("\nPress any key to exit...");
                Console.ReadKey(true);
                return;
            }
            else
            {
                charset = charset.ToUpper();
                Logger.Log(Logger.Level.Info, $"CHARSET:{charset}");
            }

            switch (charset)
            {
                case "ASCII":
                    writer.Write((byte)0);
                    break;
                default:
                    Logger.Log(Logger.Level.Fatal, $"Unsupported/unimplemented CHARSET found : {charset} is not supported.");
                    throw new NotImplementedException();
            }

            /* フォント横幅 */
            if (!parseValueTree.TryGetValue("FONT_WIDTH", out string font_width))
            {
                Logger.Log(Logger.Level.Fatal, $"Font parameter FONT_WIDTH not found. Exiting.");
                Console.Write("\nPress any key to exit...");
                Console.ReadKey(true);
                return;
            }
            else
            {
                if (!byte.TryParse(font_width, out byte fontWidth))
                {
                    Logger.Log(Logger.Level.Fatal, $"Invalid FONT_WIDTH found : {font_width} is not a valid UInt8 value.");
                    Console.Write("\nPress any key to exit...");
                    Console.ReadKey(true);
                    return;
                }
                if(fontWidth != 8 && fontWidth != 16 && fontWidth != 32 && fontWidth != 64)
                {
                    Logger.Log(Logger.Level.Fatal, $"Unsupported/unimplemented FONT_WIDTH found : {font_width} is not supported.");
                    Console.Write("\nPress any key to exit...");
                    Console.ReadKey(true);
                    return;
                }

                Logger.Log(Logger.Level.Info, $"FONT_WIDTH:{fontWidth}");
                writer.Write(fontWidth);
            }

            /* フォント高さ */
            if (!parseValueTree.TryGetValue("FONT_HEIGHT", out string font_height))
            {
                Logger.Log(Logger.Level.Fatal, $"Font parameter FONT_HEIGHT not found. Exiting.");
                Console.Write("\nPress any key to exit...");
                Console.ReadKey(true);
                return;
            }
            else
            {
                if (!byte.TryParse(font_height, out byte fontHeight))
                {
                    Logger.Log(Logger.Level.Fatal, $"Invalid FONT_HEIGHT found : {font_height} is not a valid UInt8 value.");
                    Console.Write("\nPress any key to exit...");
                    Console.ReadKey(true);
                    return;
                }

                Logger.Log(Logger.Level.Info, $"FONT_HEIGHT:{fontHeight}");
                writer.Write(fontHeight);
            }

            //総グリフ数（あとで書き込む）offset=6
            writer.Write((byte)0);

            /*デフォルト文字色*/

            byte r = 255;
            byte g = 255;
            byte b = 255;

            if (parseValueTree.TryGetValue("DEFAULT_RGB_R", out string rgbr))
            {
                if(byte.TryParse(rgbr, out r))
                {
                    Logger.Log(Logger.Level.Info, $"DEFAULT_RGB_R:{r}");
                }
                else
                {
                    Logger.Log(Logger.Level.Warning, $"Invalid DEFAULT_RGB_R found : {rgbr} is not a valid UInt8 value. Set to default value 255.");
                }
            }
            else
            {
                Logger.Log(Logger.Level.Info, "DEFAULT_RGB_R is not set. Set to default value 255.");
            }

            if (parseValueTree.TryGetValue("DEFAULT_RGB_G", out string rgbg))
            {
                if (byte.TryParse(rgbg, out g))
                {
                    Logger.Log(Logger.Level.Info, $"DEFAULT_RGB_G:{g}");
                }
                else
                {
                    Logger.Log(Logger.Level.Warning, $"Invalid DEFAULT_RGB_G found : {rgbg} is not a valid UInt8 value. Set to default value 255.");
                }
            }
            else
            {
                Logger.Log(Logger.Level.Info, "DEFAULT_RGB_G is not set. Set to default value 255.");
            }

            if (parseValueTree.TryGetValue("DEFAULT_RGB_B", out string rgbb))
            {
                if (byte.TryParse(rgbb, out b))
                {
                    Logger.Log(Logger.Level.Info, $"DEFAULT_RGB_B:{b}");
                }
                else
                {
                    Logger.Log(Logger.Level.Warning, $"Invalid DEFAULT_RGB_B found : {rgbb} is not a valid UInt8 value. Set to default value 255.");
                }
            }
            else
            {
                Logger.Log(Logger.Level.Info, "DEFAULT_RGB_B is not set. Set to default value 255.");
            }

            writer.Write(r);
            writer.Write(g);
            writer.Write(b);

            /* フォント名を取得 */
            string name = "FONT";
            if (!parseValueTree.TryGetValue("FONT_NAME", out name))
            {
                Logger.Log(Logger.Level.Info, "FONT_NAME is not set. Set to default value \"FONT\".");
            }
            Logger.Log(Logger.Level.Info, $"FONT_NAME:{name}");

            byte[] fontName = Encoding.ASCII.GetBytes(name);
            var temp = fontName.ToList();
            //null終端
            temp.Add(0);
            fontName = temp.ToArray();

            writer.Write(fontName);

            //ヘッダは作成完了
            //実装

            string? line;
            List<byte> used_idx = new();
            int d = 0;

            using (StreamReader sr = new(fontfilename))
            {
                Stopwatch watch = new();
                watch.Start();
                while((line = sr.ReadLine()) != null)
                {
                    d++;
                    if(line.Length == 0 || line.StartsWith("//"))
                    {
                        continue;
                    }

                    char ch = line[0];
                    byte idx = Encoding.ASCII.GetBytes(ch.ToString())[0];
                    if(used_idx.Contains(idx))
                    {
                        Logger.Log(Logger.Level.Warning, $"Glyph for {ch} is already defined.");
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if(eq == -1)
                    {
                        Logger.Log(Logger.Level.Warning, $"Syntax Error at line {d}:{line} is not a valid format.");
                        continue;
                    }
                    else if(eq == 0 && line[0] == '=')
                    {
                        eq = 1;
                    }

                        Logger.Log(Logger.Level.Debug, $"Compiling for char {ch}...");

                    string arr_before = line.Substring(eq + 1, line.Length - eq - 1);
                    List<string> arr = arr_before.Split(',').ToList();
                    List<ulong> res = new();
                    for (int i = 0; i < arr.Count; i++)
                    {
                        arr[i] = arr[i].Trim();
                        if (arr[i].StartsWith("0b"))
                        {
                            arr[i] = arr[i].Substring(2);
                        }
                        if (arr[i].Length != byte.Parse(font_width))
                        {
                            Logger.Log(Logger.Level.Fatal, $"Font width mismatch:expected {font_width}, got {arr[i].Length}");
                            Console.Write("\nPress any key to exit...");
                            Console.ReadKey(true);
                            return;
                        }
                        ulong bin;
                        if (!ulong.TryParse(arr[i], out ulong _))
                        {
                            Logger.Log(Logger.Level.Fatal, $"Invalid value detected:{arr[i]} is invalid value.");
                            Console.Write("\nPress any key to exit...");
                            Console.ReadKey(true);
                            return;
                        }

                        try
                        {
                            bin = ulong.Parse(arr[i], System.Globalization.NumberStyles.BinaryNumber);
                        }
                        catch(Exception e)
                        {
                            Logger.Log(Logger.Level.Fatal, $"Invalid value detected:{arr[i]} is invalid value.");
                            Console.Write("\nPress any key to exit...");
                            Console.ReadKey(true);
                            return;
                        }
                        res.Add(bin);
                    }

                    if(res.Count != byte.Parse(font_height))
                    {
                        Logger.Log(Logger.Level.Fatal, $"Binary is missing for {ch}, expected {byte.Parse(font_height)} but got {res.Count}");
                        Console.Write("\nPress any key to exit...");
                        Console.ReadKey(true);
                        return;
                    }

                    used_idx.Add(idx);

                    writer.Write(idx);
                    foreach(ulong n in res)
                    {
                        switch (byte.Parse(font_width))
                        {
                            case 8:
                                writer.Write((byte)n);
                                break;
                            case 16:
                                writer.Write((ushort)n);
                                break;
                            case 32:
                                writer.Write((uint)n);
                                break;
                            case 64:
                                writer.Write(n);
                                break;
                            default:
                                throw new System.Diagnostics.UnreachableException();
                        }
                    }
                }
                watch.Stop();

                writer.Seek(6, SeekOrigin.Begin);
                writer.Write((byte)used_idx.Count);

                Console.WriteLine($"Compilation succeeded. Time elapsed:{watch.Elapsed.TotalMilliseconds} ms. Compiled characters:");
                foreach(byte n in used_idx)
                {
                    Console.Write((char)n);
                    if(n != used_idx.Last())
                    {
                        Console.Write(", ");
                    }
                }

                Console.Write("\nPress any key to exit...");
                Console.ReadKey(true);
            }
        }        
    }
}