using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HP.HPTRIM.SDK;

namespace TRIMRevisionExtract
{
    internal partial class Program
    {
        public static void DoTrimAuditStuff(Database db)
        {
            string strMainAuditImportFile = string.Format(strAuditOutfile, strSitePrefix, "Import");

            string directoryName = Path.GetDirectoryName(strMainAuditImportFile);
            string filename = Path.GetFileName(strMainAuditImportFile);

            if (!Directory.Exists(directoryName))
            {
                Console.WriteLine("Creating path {0}.", directoryName);
                swPerfAndLogOutfile.WriteLine("Creating path {0}.", directoryName);
                DirectoryInfo di = Directory.CreateDirectory(directoryName);
            }

            swAuditOutfile = new StreamWriter(strMainAuditImportFile);
            swAuditOutfile.AutoFlush = true;

            swPerfAndLogOutfile.WriteLine("Writing to Main Import file: {0}", strMainAuditImportFile);

            // Connect to Saved Search here .. 
            SavedSearch ss = new SavedSearch(db, strSavedSearch);
            TrimMainObjectSearch objSearch = ss.GetSearch();

            _db = db;

            long numObjects = objSearch.Count;
            //float recs_per_sec = 2.6F;
            long estimate = numObjects / 160;

            swAuditOutfile.WriteLine("SourcePath,DestinationPath,Title,Created By,Created");

            Console.WriteLine("DoTrimAuditStuff: retrieving [{0}]", objSearch.SearchString);
            swPerfAndLogOutfile.WriteLine("DoTrimAuditStuff: retrieving [{0}]", objSearch.SearchString);

            foreach (Record objRec in objSearch)
            {
                GetAuditHistoryRecursive(objRec);
            }

            if (objSearch != null)
            {
                // dispose??
            }

            swAuditOutfile.Close();
        }

        private static void GetAuditHistoryRecursive(Record objRec)
        {
            if (lstContainerExclusionList.BinarySearch(objRec.Number) >= 0)
            {
                swPerfAndLogOutfile.WriteLine("GetAuditHistoryRecursive: Excluding record number: {0}", objRec.Name);
                Console.WriteLine("GetAuditHistoryRecursive: Excluding record number: {0}", objRec.Name);
                return;
            }

            GetAuditHistoryForRecord(objRec);

            if (objRec.IsContainer)
            {
                TrimMainObjectSearch objSearch = new TrimMainObjectSearch(_db, BaseObjectTypes.Record);
                string strSearchClause = string.Format("container:[number:{0}]", objRec.Number);
                objSearch.SetSearchString(strSearchClause);
                //objSearch.SelectAll();

                foreach (Record rec in objSearch)
                {
                    GetAuditHistoryRecursive(rec);
                }
            }
        }

        private static void GetAuditHistoryForRecord(Record objRec)
        {
            Console.WriteLine("{0}: Doing record number: {1}", ++gRecCount, objRec.Name);
            swPerfAndLogOutfile.WriteLine("{0}: Doing record number: {1}", gRecCount, objRec.Name);

            // get all document payload associated with this record
            if (objRec != null)
            {
                string outputFile = GetFullHistory(objRec);
                if (outputFile == string.Empty)
                {
                    return;
                }

                // need to define this as a variable in the config file

                string outline = String.Format("{0},{1},{2},{3},{4}",
                    outputFile,
                    "Audit Event Files\\" + Path.GetFileName(outputFile),
                    SanitiseFieldValue(objRec.Title),
                    SanitiseFieldValue(objRec.Creator.Name),
                    objRec.DateCreated);

                swAuditOutfile.WriteLine(outline);
            }
        }

        static void GetShortHistory(Record rec, string propName, ref string theValue)
        {
            if (strDebug == "debug2")
            {
                Console.WriteLine("Audit event data: {0} audit events for record {1}.", theValue, rec.Number);
                swPerfAndLogOutfile.WriteLine("Audit event data: {0} audit events for record {1}.", theValue, rec.Number);
            }

            theValue = theValue.Replace("\"", "'");
            theValue = theValue.Replace("; ", "\r\n");
            theValue = "\"" + theValue + "\"";
        }

        static string GetFullHistory(Record rec)
        {
            TrimMainObjectSearch srch = new TrimMainObjectSearch(_db, BaseObjectTypes.History);
            srch.SetFilterString("object:record," + rec.Uri);
            srch.SelectAll();

            if (srch.Count == 0)
            {
                return string.Empty;
            }

            string name = rec.Number.Replace('/', '_').Replace('\\', '_');
            string strRecAuditImportFile = string.Format(strAuditOutfile, name, rec.Uri);
            StreamWriter swRecAuditOutfile = new StreamWriter(strRecAuditImportFile);
            swRecAuditOutfile.AutoFlush = true;

            swPerfAndLogOutfile.WriteLine("Writing to Rec Import file: {0}", strRecAuditImportFile);

            swRecAuditOutfile.WriteLine("Event,For Object Uri,Record Number,Updated By,Event Date,EventDescription");

            if (strDebug == "debug2")
            {
                Console.WriteLine("There are {0} audit events for record {1}.", srch.Count, rec.Number);
                swPerfAndLogOutfile.WriteLine("There are {0} audit events for record {1}.", srch.Count, rec.Number);
            }

            foreach (History h in srch)
            {
                try
                {
                    string hs = SanitiseFieldValue(h.EventDescription);
                    string strEvent = hs.Substring(0, hs.IndexOf(" - "));

                    Location lub = h.LastUpdatedBy;
                    string lubName = lub?.Name;
                    string strUpdater = SanitiseFieldValue(lub.Name);
                    string strDateUpdated = h.LastUpdatedOn.ToString();

                    swRecAuditOutfile.WriteLine("{0},{1},{2},{3},{4},{5}", strEvent, h.ForObjectUri, rec.Number, strUpdater, strDateUpdated, hs);
                }
                catch (Exception ex)
                {
                    swRecAuditOutfile.WriteLine("Exception caught, sorry!");
                    swPerfAndLogOutfile.WriteLine("Exception caught: Rec: {0}, {1}", rec.Uri, rec.Number);
                    swPerfAndLogOutfile.WriteLine("excetion was: ",ex.ToString());
                }
            }

            swRecAuditOutfile.Close();

            return strRecAuditImportFile;
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
