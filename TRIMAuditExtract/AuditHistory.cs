using System;
using System.IO;
using HP.HPTRIM.SDK;

namespace TRIMAuditExtract
{
    internal partial class Program
    {
        static Database _db = null;
        public static void DoTrimAuditStuff(Database db)
        {
            string directoryName = Path.GetDirectoryName(strAuditOutfile);
            string filename = Path.GetFileName(strAuditOutfile);

            if (!Directory.Exists(directoryName))
            {
                Console.WriteLine("Creating path {0}.", directoryName);
                swPerfAndLogOutfile.WriteLine("Creating path {0}.", directoryName);
                DirectoryInfo di = Directory.CreateDirectory(directoryName);
            }

            swAuditOutfile = new StreamWriter(strAuditOutfile);
            swAuditOutfile.AutoFlush = true;

            srAuditInputfile = new StreamReader(strAuditInputfile);

            swPerfAndLogOutfile.WriteLine("Writing to Main Audit file: {0}", strAuditOutfile);
            _db = db;

            swAuditOutfile.WriteLine("Event,For Object Uri,Record Number,Updated By,Event Date,EventDescription");

            string uri = srAuditInputfile.ReadLine();
            while (!srAuditInputfile.EndOfStream)
            {
                GetFullHistory(uri, swAuditOutfile);
                uri = srAuditInputfile.ReadLine();
            }
            swAuditOutfile.Close();
            srAuditInputfile.Close();
        }
        static void GetFullHistory(string szRecUri, StreamWriter swRecAuditOutfile)
        {
            TrimMainObjectSearch srch = new TrimMainObjectSearch(_db, BaseObjectTypes.History);
            srch.SetFilterString("object:record," + szRecUri);
            srch.SelectAll();

            if (srch.Count == 0)
            {
                swPerfAndLogOutfile.WriteLine("No Audit events found for uri: {0}", szRecUri);

                return;
            }

            if (strDebug == "debug2")
            {
                Console.WriteLine("There are {0} audit events for record {1}.", srch.Count, szRecUri);
                swPerfAndLogOutfile.WriteLine("There are {0} audit events for record {1}.", srch.Count, szRecUri);
            }

            foreach (History h in srch)
            {
                try
                {
                    string hs = SanitiseFieldValue(h.EventDescription);
                    string strEvent = hs.Substring(0, hs.IndexOf(" - "));

                    Location lub = h.LastUpdatedBy;
                    string lubName = lub?.Name;
                    string strUpdater = SanitiseFieldValue(lubName);
                    string strDateUpdated = h.LastUpdatedOn.ToString();

                    swRecAuditOutfile.WriteLine("{0},{1},{2},{3},{4},{5}", strEvent, h.ForObjectUri, szRecUri, strUpdater, strDateUpdated, hs);
                }
                catch (Exception ex)
                {
                    swRecAuditOutfile.WriteLine("Exception caught, sorry!");
                    swPerfAndLogOutfile.WriteLine("Exception caught: Rec: {0}", szRecUri);
                }
            }

        }

        private static string SanitiseFieldValue(string theValue)
        {
            if (theValue == null)
            { 
                return string.Empty;
            }
            theValue = theValue.Replace('"', '\'');
            theValue = theValue.Contains(",") ? "\"" + theValue + "\"" : theValue;
            theValue = theValue.Replace('\n', ' ');
            theValue = theValue.Replace('\r', ' ');
            
            return theValue;
        }

    }
}
