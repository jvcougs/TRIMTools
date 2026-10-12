using System;
using System.Configuration;
using System.IO;
using HP.HPTRIM.SDK;
using System.Diagnostics;

namespace TRIMAuditExtract
{
    internal partial class Program
    {
        static string strUsername = "Not found";
        static string strPassword = "Not found";
        static string strLoginMethod = string.Empty;
        static string strExtractBaseDir = string.Empty;
        static string strTrimInstallDir = string.Empty;
        static string strWGS = string.Empty;
        static string strDBId = string.Empty;
        static string strPerfAndLogOutfile = string.Empty;
        static string strAuditOutfile = string.Empty;
        static string strAuditInputfile = string.Empty;
        static string strInputFileFilterFormat = string.Empty;
        static string strExtractBaseDirectory = string.Empty;
        
        static string strDebug = string.Empty;

        static string TrimExtractAuditOnly = string.Empty;

        static bool doIA = false;

        static Stopwatch sw = new Stopwatch();
        static StreamWriter swPerfAndLogOutfile;
        static StreamWriter swAuditOutfile;
        static StreamReader srAuditInputfile;

        static long gRecCount = 0;
        static long gActualRecordCount = 0;
        static long gErrorRecordCount = 0;
        static long gRecordsWithRevisions = 0;

        static void Main(string[] args)
        {
            sw.Start();

            Init();

            swPerfAndLogOutfile = new StreamWriter(strPerfAndLogOutfile);
            swPerfAndLogOutfile.AutoFlush = true;

            try
            {
                using (Database db = new Database())
                {
                    db.Id = strDBId;
                    db.AutoConnect = false;
                    db.AuthenticationMethod = ClientAuthenticationMechanism.ExplicitWindows;
                    db.WorkgroupServerName = strWGS;
                    db.WorkgroupServerPort = 1137;

                    Console.WriteLine("Trying Connect() to {0}", strDBId);
                    try
                    {
                        Console.WriteLine("Connecting to {0}", strWGS);
                        swPerfAndLogOutfile.WriteLine("Connecting to {0}", strWGS);
                        //db.ConnectAs(strUsername, strPassword);
                        if (strLoginMethod == "ExplicitWindows")
                        {
                            Console.WriteLine("Connecting using Explicit Windows");
                            swPerfAndLogOutfile.WriteLine("Connecting using Explicit Windows");

                            db.AuthenticationMethod = ClientAuthenticationMechanism.ExplicitWindows;
                        }
                        else if (strLoginMethod == "IntegratedWindows")
                        {
                            Console.WriteLine("Connecting using Integrated Windows Authentication.");
                            swPerfAndLogOutfile.WriteLine("Connecting using Integrated Windows Authentication.");

                            db.AuthenticationMethod = ClientAuthenticationMechanism.IntegratedWindows;
                        }

                        if (strUsername != "Not found" && strPassword != "Not found")
                        {
                            db.ConnectAs(strUsername, strPassword);
                        }

                        if (!db.IsConnected)
                        {
                            db.AutoConnect = true;
                            db.Connect();
                        }

                        Console.WriteLine("DB Id=" + db.Id);
                        swPerfAndLogOutfile.WriteLine("DB Id=" + db.Id);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        //Console.WriteLine(ex.
                    }

                    if (db.IsConnected)
                    {
                        DoTrimAuditStuff(db);
                    }
                }
            }
            catch(Exception ex) 
            {
                Console.WriteLine(ex.ToString());
                swPerfAndLogOutfile.WriteLine(ex.ToString() );
            }

            sw.Stop();

            Console.WriteLine("Elapsed Time={0}, Record Count={1}", sw.Elapsed, gRecCount);
            swPerfAndLogOutfile.WriteLine("Elapsed Time={0}, Record Count={1}", sw.Elapsed, gRecCount);
            swPerfAndLogOutfile.Close();

            Console.Write("Press a key to exit: ");
            Console.ReadKey();
        }

        static void Init()
        {
            strDebug = ConfigTools.GetAppSetting("Debug");

            strLoginMethod = ConfigTools.GetAppSetting("LoginMethod");

            strUsername = ConfigTools.GetAppSetting("Username");
            strPassword = ConfigTools.GetAppSetting("Password");

            strWGS = ConfigTools.GetAppSetting("WGS");

            strExtractBaseDir = ConfigTools.GetAppSetting("ExtractBaseDirectory");
            strTrimInstallDir = ConfigTools.GetAppSetting("TRIM Install Dir");
            strDBId = ConfigTools.GetAppSetting("TRIM DBId");

            strPerfAndLogOutfile = ConfigTools.GetAppSetting("PerfAndLogOutfile");
            strAuditOutfile = ConfigTools.GetAppSetting("AuditOutfile");
            strAuditInputfile = ConfigTools.GetAppSetting("AuditInputfile");

            strInputFileFilterFormat = ConfigTools.GetAppSetting("InputFileFilterFormat");
            
            string temp = Environment.GetEnvironmentVariable("PATH") + ":" + strTrimInstallDir;
            Environment.SetEnvironmentVariable("PATH", temp);

            TrimApplicationBase.TrimBinariesLoadPath = strTrimInstallDir;
            TrimApplication.Initialize();

            strExtractBaseDirectory = ConfigTools.GetAppSetting("ExtractBaseDirectory");

            TrimExtractAuditOnly = ConfigTools.GetAppSetting("TrimExtractAuditOnly");
        }
    }
}
