using Microsoft.Extensions.Configuration;

namespace TopMail.Rest.Services;

public class LogFileWriter
{
    private readonly string _logLevel;

    public LogFileWriter(IConfiguration configuration)
    {
        _logLevel = configuration["logFile"] ?? "TUTTO";
    }

    public void Write(string mittente, string destinatario, string oggetto, string tipoOperazione, string wm, string ext1, string ext2)
    {
        if (_logLevel == "NIENTE") return;

        try
        {
            var path = "c:/temp/{0}_topmail.log"; // intentionally mirror original path pattern
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (_logLevel == "TUTTO")
            {
                if (tipoOperazione == "chiamataMetodo")
                {
                    File.AppendAllText(path,
                        string.Format("WEBMETHOD: {1} DATA:{2} - START: da {3} a {4}: {5}\n",
                            DateTime.Today.ToShortDateString(), wm, DateTime.Now.ToString("yyyyMMdd hhmiss"),
                            mittente, destinatario, oggetto));
                }
                else if (tipoOperazione == "fineMetodo")
                {
                    File.AppendAllText(path,
                        string.Format("WEBMETHOD: {1} DATA:{2} - END: da {3} a {4}: {5} _____ RESULT:{6} \n",
                            DateTime.Today.ToShortDateString(), wm, DateTime.Now.ToString("yyyyMMdd hhmiss"),
                            mittente, destinatario, oggetto, ext1));
                }
                else if (tipoOperazione == "errore")
                {
                    File.AppendAllText(path,
                        string.Format("WEBMETHOD: {1} DATA:{2} - ERROR: da {3} a {4}: {5} _____ MESSAGE:{6}\n{7} \n",
                            DateTime.Today.ToShortDateString(), wm, DateTime.Now.ToString("yyyyMMdd hhmiss"),
                            mittente, destinatario, oggetto, ext1, ext2));
                }
            }
            else if (_logLevel == "ERRORI")
            {
                if (tipoOperazione == "errore")
                {
                    File.AppendAllText(path,
                        string.Format("WEBMETHOD: {1} DATA:{2} - ERROR: da {3} a {4}: {5} _____ MESSAGE:{6}\n{7} \n",
                            DateTime.Today.ToShortDateString(), wm, DateTime.Now.ToString("yyyyMMdd hhmiss"),
                            mittente, destinatario, oggetto, ext1, ext2));
                }
            }
        }
        catch
        {
            // swallow logging errors to not block business flow
        }
    }
}