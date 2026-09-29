using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using HP.HPTRIM.SDK;
using System.Diagnostics;
using System.Data.SqlTypes;
using System.Runtime.Remoting;
using System.Linq;
using System.Collections;


namespace TRIMRevisionExtract
{
    internal partial class Program
    {
        static string strLoginMethod = string.Empty;
        static string strUsername = string.Empty;
        static string strPassword = string.Empty;
        static string strSavedSearch = string.Empty;

        static string strSitePrefix = string.Empty; 
        static string strContainerExclusionList = string.Empty; 

        static string strExtractBaseDir = string.Empty;
        static string strTrimInstallDir = string.Empty;
        static string strRPRecordHeaders = string.Empty;
        static string strRPFileHeaders = string.Empty;
        static string strWGS = string.Empty;
        static string strDBId = string.Empty;
        static string strRecordsOutfile = string.Empty;
        static string strContainerOutfile = string.Empty;
        static string strPerfAndLogOutfile = string.Empty;
        static string strCheckListFile = string.Empty;
        static string strAuditOutfile = string.Empty;
        static string strExtractBaseDirectory = string.Empty;
        
        
        static string strDebug = string.Empty;

        static string TrimExtractMetadata = string.Empty;
        static string TrimExtractDocuments = string.Empty;
        static string TrimExtractAuditOnly = string.Empty;

        static string TrimExtractUseDocStoreMap = string.Empty;

        static string IA_Options_DoTrimWalk = string.Empty;
        static string IA_ExtractACLs = string.Empty;
        static string IA_ExtractRecordTypes = string.Empty;
        static string IA_ExtractClassifications = string.Empty; 
        static string IA_ExtractSecurity = string.Empty;
        static string IA_ExtractDocStores = string.Empty;
        static string IA_DoRawCount = string.Empty;
        static string IA_DoDumpAuditHistory = string.Empty;
        static string IA_DoRevisionHistogram = string.Empty;
        static string IA_OutputFile = string.Empty;

        static bool doIA = false;

        static Dictionary<string, string> recordHeaderTable = new Dictionary<string, string>();
        static Dictionary<string, string> recordValueTable = new Dictionary<string, string>();
        static Dictionary<string, string> fileHeaderTable = new Dictionary<string, string>();
        static Dictionary<string, string> fileValueTable = new Dictionary<string, string>();


        static List<string> lstContainerExclusionList = new List<string>();

        static Stopwatch sw = new Stopwatch();
        static StreamWriter swPerfAndLogOutfile;
        static StreamWriter swCheckListFile;
        static StreamWriter swRecordsOutfile;
        static StreamWriter swContainerOutfile;
        static StreamWriter swAuditOutfile;

        static IA_Tools ia_tools = null;
        static CheckList checkList = null;

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
                    // Connect to Default DB here .. we'll want to make this configurable via config file

                    db.Id = strDBId;
                    db.AutoConnect = false;
                    db.WorkgroupServerName = strWGS;
                    db.WorkgroupServerPort = 1137;
                    //db.

                    Console.WriteLine("Trying Connect() to {0}", strDBId);
                    try
                    {

                        Console.WriteLine("Connecting to {0}", strWGS);
                        swPerfAndLogOutfile.WriteLine("Connecting to {0}", strWGS);

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

                        if (strUsername == "Not Found" && strPassword == "Not Found")
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
                        if (doIA)
                        {
                            ia_tools.SetDatabase(db);
                            ia_tools.DoTrimIAStuff(sw);
                        }

                        if (TrimExtractMetadata == "true" || TrimExtractDocuments == "true")
                        {
                            db.UseClientDocumentCache = false;
                            db.UseServerDocumentCache = false;
                            db.RefreshCache();

                            DoTrimStuff(db);

                            Console.WriteLine("Actual Record Count={0}, RecordWithRevisionCount={1}", gActualRecordCount, gRecordsWithRevisions);
                            swPerfAndLogOutfile.WriteLine("Actual Record Count={0}, RecordWithRevisionCount={1}", gActualRecordCount, gRecordsWithRevisions);

                        }
                        else if (TrimExtractAuditOnly == "true")
                        {
                            DoTrimAuditStuff(db);
                        }
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

        //static Dictionary<int, BaseObjectTypes.RecordType> recTypes = new Dictionary<int, BaseObjectTypes.RecordType>();


        static void Init()
        {
            strDebug = ConfigTools.GetAppSetting("Debug");
            strDebug = ConfigTools.GetAppSetting("Debug");

            strUsername = ConfigTools.GetAppSetting("Username");
            strPassword = ConfigTools.GetAppSetting("Password");

            strLoginMethod = ConfigTools.GetAppSetting("LoginMethod");

            strWGS = ConfigTools.GetAppSetting("WGS");
            strSitePrefix = ConfigTools.GetAppSetting("Site Prefix");
            strContainerExclusionList = ConfigTools.GetAppSetting("ContainerExclusionList");

            if (strContainerExclusionList != null && strContainerExclusionList.Length > 0)
            {
                foreach (string key in strContainerExclusionList.Split(','))
                {
                    lstContainerExclusionList.Add(key);
                }
            }

            if (strDebug == "debug")
            {
                foreach(string x in lstContainerExclusionList)
                {
                    Console.WriteLine("Excluding: [{0}]", x);
                }
            }

            strSavedSearch = ConfigTools.GetAppSetting("Saved Search");

            strExtractBaseDir = ConfigTools.GetAppSetting("ExtractBaseDirectory");
            strTrimInstallDir = ConfigTools.GetAppSetting("TRIM Install Dir");
            strDBId = ConfigTools.GetAppSetting("TRIM DBId");

            strRecordsOutfile = ConfigTools.GetAppSetting("RecordsOutfile");
            strContainerOutfile = ConfigTools.GetAppSetting("ContainerOutfile");
            strPerfAndLogOutfile = ConfigTools.GetAppSetting("PerfAndLogOutfile");
            strCheckListFile = ConfigTools.GetAppSetting("CheckListFile");
            strAuditOutfile = ConfigTools.GetAppSetting("AuditOutfile");

            if (strSitePrefix != string.Empty)
            {
                strSitePrefix = strSitePrefix.Replace("/", "_");
                strRecordsOutfile = String.Format(strRecordsOutfile, strSitePrefix);
                strContainerOutfile = String.Format(strContainerOutfile, strSitePrefix);
                strPerfAndLogOutfile = String.Format(strPerfAndLogOutfile, strSitePrefix);
                strCheckListFile = String.Format(strCheckListFile, strSitePrefix);
                strAuditOutfile = String.Format(strAuditOutfile, strSitePrefix, "{0}", "{1}");
            }

            string TRIMFolderFields = ConfigurationManager.AppSettings["TRIMFolderFields"];
            string[] fileTRIMHeaders = TRIMFolderFields.Split(',');
            strRPFileHeaders = ConfigurationManager.AppSettings["RPBaseFolderFields"];
            string[] fileRPHeaders = strRPFileHeaders.Split(',');

            string TRIMRecordFields = ConfigurationManager.AppSettings["TRIMRecordFields"];
            string[] recordTRIMHeaders = TRIMRecordFields.Split(',');
            strRPRecordHeaders = ConfigurationManager.AppSettings["RPBaseRecordFields"];
            string[] recordRPHeaders = strRPRecordHeaders.Split(',');

            int i = 0;
            foreach (string line in fileRPHeaders)
            {
                fileHeaderTable.Add(fileTRIMHeaders[i++], line);
                fileValueTable.Add(line, "");
            }

            i = 0;
            foreach (string line in recordRPHeaders)
            {
                recordHeaderTable.Add(recordTRIMHeaders[i++], line);
                recordValueTable.Add(line, "");
            }

            string temp = Environment.GetEnvironmentVariable("PATH") + ":" + strTrimInstallDir;
            Environment.SetEnvironmentVariable("PATH", temp);

            TrimApplicationBase.TrimBinariesLoadPath = strTrimInstallDir;
            TrimApplication.Initialize();

            strExtractBaseDirectory = ConfigTools.GetAppSetting("ExtractBaseDirectory");

            TrimExtractMetadata = ConfigTools.GetAppSetting("TrimExtractMetadata");
            TrimExtractDocuments = ConfigTools.GetAppSetting("TrimExtractDocuments");
            TrimExtractAuditOnly = ConfigTools.GetAppSetting("TrimExtractAuditOnly");

            TrimExtractUseDocStoreMap = ConfigTools.GetAppSetting("TrimExtractUseDocStoreMap");

            IA_Options_DoTrimWalk = ConfigTools.GetAppSetting("IA_Options_DoTrimWalk");
            IA_ExtractACLs = ConfigTools.GetAppSetting("IA_ExtractACLs");
            IA_ExtractRecordTypes = ConfigTools.GetAppSetting("IA_ExtractRecordTypes");
            IA_ExtractClassifications = ConfigTools.GetAppSetting("IA_ExtractClassifications");
            IA_ExtractDocStores = ConfigTools.GetAppSetting("IA_ExtractDocStores");
            IA_DoRawCount = ConfigTools.GetAppSetting("IA_DoRawCount");
            IA_DoDumpAuditHistory = ConfigTools.GetAppSetting("IA_DoDumpAuditHistory");
            IA_DoRevisionHistogram = ConfigTools.GetAppSetting("IA_DoRevisionHistogram");
            IA_OutputFile = ConfigTools.GetAppSetting("IA_OutputFile");

            LoadEStoreMap();

            if (TrimExtractMetadata == "true" && File.Exists(strRecordsOutfile) && File.Exists(strContainerOutfile))
            {
                //checkList = new CheckList(strContainerOutfile, strRecordsOutfile, TRIMFolderFields, TRIMRecordFields);
                checkList = new CheckList(strContainerOutfile, strRecordsOutfile, strCheckListFile, strRPFileHeaders, strRPRecordHeaders);
            }

            doIA = IA_Options_DoTrimWalk == "true" ||
                IA_ExtractACLs == "true" ||
                IA_ExtractRecordTypes == "true" ||
                IA_ExtractClassifications == "true" ||
                IA_ExtractDocStores == "true" ||
                IA_DoRawCount == "true" ||
                IA_DoDumpAuditHistory == "true" ||
                IA_DoRevisionHistogram == "true";

            if (doIA)
            {
                ia_tools = new IA_Tools(strExtractBaseDirectory);
                ia_tools.IA_Options_DoTrimWalk = IA_Options_DoTrimWalk;
                ia_tools.IA_ExtractACLs = IA_ExtractACLs;
                ia_tools.IA_ExtractRecordTypes = IA_ExtractRecordTypes;
                ia_tools.IA_ExtractClassifications = IA_ExtractClassifications;
                ia_tools.IA_ExtractSecurity = IA_ExtractSecurity;
                ia_tools.IA_ExtractDocStores = IA_ExtractDocStores;
                ia_tools.IA_DoRawCount = IA_DoRawCount;
                ia_tools.IA_DumpAuditHistory = IA_DoDumpAuditHistory;
                ia_tools.IA_DoRevisionHistogram = IA_DoRevisionHistogram;
                ia_tools.strSavedSearch = strSavedSearch;
                ia_tools.IA_OutputFile = IA_OutputFile;
            }
        }
    }
}
