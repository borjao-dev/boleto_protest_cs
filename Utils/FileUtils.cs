namespace BoletoProtest.Utils;

public class FileUtils
{
    public static void MakeDir(string path)
    {
        Directory.CreateDirectory(path);
    }

    public static string FormatDate(string date)
    {
        return date.Replace("/", "-");
    }
}