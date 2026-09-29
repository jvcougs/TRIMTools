using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using System.IO;
using CsvHelper.Configuration.Attributes;
using HP.HPTRIM.SDK;
using System.Web;

namespace TRIMRevisionExtract
{
    internal class CheckList
    {
        public string strContainerFile = string.Empty;
        public string strRecordsFile = string.Empty;

        public string strRPFileHeaders = string.Empty;
        public string strRPRecordHeaders = string.Empty;

        static Dictionary<string, string> checkList = new Dictionary<string, string>();

        public bool isLoaded = false;
        private class ContainerRec
        {
            //
            //DestinationPath,Title,Number,DateCreated,RetentionSchedule,Assignee,OwnerLocation,HomeLocation,DateRegistered,Creator,LastUpdatedOn,Uri,DateClosed,History
            public string ContainerHierarchy { get; set; }
            public string Title { get; set; }
            public string Number { get; set; }
            public string DateCreated { get; set; }
            public string RetentionSchedule { get; set; }
            public string Assignee { get; set; }
            public string OwnerLocation { get; set; }
            public string HomeLocation { get; set; }
            public string DateRegistered { get; set; }
            public string Creator { get; set; }
            public string LastUpdatedOn { get; set; }
            public string Uri { get; set; }
            public string DateClosed { get; set; }
            public string History { get; set; }
        }

        void DumpContainerRec(ContainerRec rec, StreamWriter sw)
        {
            sw.WriteLine($"{rec.ContainerHierarchy},\"{rec.Title}\",{rec.Number},{rec.DateCreated},{rec.RetentionSchedule},\"{rec.Assignee}\",\"{rec.OwnerLocation}\",\"{rec.HomeLocation}\",{rec.DateRegistered},\"{rec.Creator}\",{rec.LastUpdatedOn},{rec.Uri},{rec.DateClosed},\"{rec.History}\"");
        }
        //SourcePath,DestinationPath,Version,Title,Number,DateCreated,Assignee,OwnerLocation,HomeLocation,DateRegistered,Author,Container,
        //Addressee,ManualDestructionDate,Notes,LastUpdatedOn,Uri,DateClosed,History
        private class RecordRec
        {
            public string SourcePath { get; set; }
            public string DestinationPath { get; set; }
            public string Version { get; set; }
            public string Title { get; set; }
            public string Number { get; set; }
            public string DateCreated { get; set; }
            //public string Creator { get; set; }
            public string Assignee { get; set; }
            public string OwnerLocation { get; set; }
            public string HomeLocation { get; set; }
            public string DateRegistered { get; set; }
            public string Author { get; set; }
            public string Container { get; set; }
            public string Addressee { get; set; }
            public string ManualDisposalDate { get; set; }
            public string Notes { get; set; }
            public string LastUpdatedOn { get; set; }
            public string Uri { get; set; }
            public string DateClosed { get; set; }
            public string History { get; set; }
        }

        void DumpRecordRec(RecordRec rec, StreamWriter sw)
        {
            //SourcePath,DestinationPath,Version,Title,Number,DateCreated,Assignee,OwnerLocation,HomeLocation,DateRegistered,Author,Container,
            //Addressee,ManualDestructionDate,Notes,LastUpdatedOn,Uri,DateClosed,History

            //sw.WriteLine($"{rec.SourcePath},{rec.DestinationPath},{rec.Version},\"{rec.Title}\",{rec.Number},{rec.DateCreated},\"{rec.Creator}\",\"{rec.Assignee}\",\"{rec.OwnerLocation}\",\"{rec.HomeLocation}\",{rec.DateRegistered},\"{rec.Author}\",{rec.Container},\"{rec.Addressee}\",{rec.ManualDisposalDate},\"{rec.Notes}\",{rec.LastUpdatedOn},{rec.Uri},{rec.DateClosed},\"{rec.History}\"");
            sw.WriteLine($"{rec.SourcePath},{rec.DestinationPath},{rec.Version},\"{rec.Title}\",{rec.Number},{rec.DateCreated},\"{rec.Assignee}\",\"{rec.OwnerLocation}\",\"{rec.HomeLocation}\",{rec.DateRegistered},\"{rec.Author}\",{rec.Container},\"{rec.Addressee}\",{rec.ManualDisposalDate},\"{rec.Notes}\",{rec.LastUpdatedOn},{rec.Uri},{rec.DateClosed},\"{rec.History}\"");
        }

        public CheckList(string ContainerFile, string RecordsFile, string CHeaders, string RHeaders) 
        {
            strContainerFile = ContainerFile;
            strRecordsFile = RecordsFile;

            strRPFileHeaders = CHeaders;
            strRPRecordHeaders = RHeaders;

            LoadFiles();
        }

        public CheckList(string ContainerFile, string RecordsFile, string CheckListFile, string CHeaders, string RHeaders)
        {
            strContainerFile = ContainerFile;
            strRecordsFile = RecordsFile;

            strRPFileHeaders = CHeaders;
            strRPRecordHeaders = RHeaders;

            LoadFiles();
            RefactorDataFiles(strRecordsFile, strContainerFile);
            //LoadCheckList(CheckListFile);
        }


        public bool doCheckUri(string Uri, ref long globalCounter)
        {
            if (isLoaded == false)
            {
                return false;
            }

            bool bRetVal = checkList.ContainsKey(Uri);
            
            if (bRetVal == true)
            {
                globalCounter++;
                checkList.Remove(Uri);
            }

            return bRetVal;
        }

        public bool doCheckNumber(string number)
        {
            if (isLoaded == false)
            {
                return false;
            }

            bool bRetVal = checkList.ContainsKey(number);

            if (bRetVal == true)
            {
                checkList.Remove(number);
            }

            return bRetVal;
        }

        private string RenameDataFile(string strFilename)
        {
            string strPath = Path.GetDirectoryName(strFilename);
            string strFileBasename = Path.GetFileNameWithoutExtension(strFilename);
            string strFileExt = Path.GetExtension(strFilename);

            int increment = 0;

            string NewName = string.Format("{0}\\{1}-{2}{3}", strPath, strFileBasename, increment, strFileExt);

            while (File.Exists(NewName))
            {
                NewName = string.Format("{0}\\{1}-{2}{3}", strPath, strFileBasename, increment++, strFileExt);
            }

            File.Move(strFilename, NewName);

            return NewName;
        }
        private void RenameDataFiles()
        {
            if (File.Exists(strContainerFile) && File.Exists(strRecordsFile))
            {
                Console.WriteLine("Output files exist! These will be renamed.");

                string strCon = RenameDataFile(strContainerFile);
                string strRec = RenameDataFile(strRecordsFile);

                Console.WriteLine("Output files: Container: {0}, Records: {1}", strCon, strRec);
            }

        }

        private void LoadCheckList(string CheckListFile)
        {
            long lRecordCount = 0;
            
            Console.Write("Checklist File exists! Load {0} into the CheckList? [Y/n]", CheckListFile);

            ConsoleKeyInfo ki = Console.ReadKey();

            if (!(ki.Key == ConsoleKey.Enter || ki.Key == ConsoleKey.Y))
            {
                return;
            }
            
            RenameDataFiles();

            Console.WriteLine();
            StreamReader streamReader = new StreamReader(CheckListFile);
            string line;
            while ((line = streamReader.ReadLine()) != null)
            {
                lRecordCount++;
                checkList.Add(line, null);
            }
            
            Console.WriteLine("Checklist File loaded! {0} values loaded into the CheckList.", lRecordCount);
            streamReader.Close();
            isLoaded = true;
        }
        private void LoadFiles()
        {
            long lContainerCount = 0;
            long lRecordCount = 0;

            if (File.Exists(strContainerFile))
            {
                string basePath = Path.GetDirectoryName(strContainerFile);
                try
                {
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        MissingFieldFound = null, // Ignore missing fields
                        BadDataFound = context => Console.WriteLine($"Bad data: {context.RawRecord}")
                    };

                    var reader = new StreamReader(strContainerFile);
                    var csv = new CsvReader(reader, config);

                    Console.WriteLine($"Loading Containers ... ");
                    var records = csv.GetRecords<ContainerRec>();

                    foreach (var rec in records)
                    {
                        lContainerCount++;
                        Console.WriteLine($"Uri: {rec.Uri}, Number: {rec.Number}");

                        DateTime dt = DateTime.Parse(rec.DateCreated);
                        if (!ContainerBuckets.ContainsKey(dt.Year.ToString()))
                        {
                            ContainerBuckets.Add(dt.Year.ToString(), new StreamWriter($"{basePath}\\{dt.Year}-Containers.csv"));
                            ContainerBuckets[dt.Year.ToString()].WriteLine(strRPFileHeaders);
                            ContainerBuckets[dt.Year.ToString()].AutoFlush = true;
                        }

                        ContainersDict.Add(rec.Uri, PrefixQuarter(rec));
                    }

                    reader.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading CSV: {ex.Message}");
                }

            }

            lRecordCount = LoadCSVRecords(0, strRecordsFile);

            if (checkList.Count > 0)
            {
                isLoaded = true;
                Console.WriteLine("checkList loaded Containers={0}, records={1}", lContainerCount, lRecordCount);
            }

            Console.WriteLine("checkList loaded ...");

        } // LoadFiles

        SortedDictionary<string, RecordRec> RecordsDict = new SortedDictionary<string, RecordRec>();
        SortedDictionary<string, ContainerRec> ContainersDict = new SortedDictionary<string, ContainerRec>();
        Dictionary<string,StreamWriter> ContainerBuckets = new Dictionary<string, StreamWriter>();
        Dictionary<string,StreamWriter> RecordBuckets = new Dictionary<string, StreamWriter>();
        public void RefactorDataFiles(string strRecordsFile, string strContainerFile)
        {
            if (File.Exists(strContainerFile) && File.Exists(strRecordsFile))
            {
                //Console.WriteLine("Output files exist! If you're loading these into checklist, make sure you've swapped the headers.");
                //Console.Write("Files exist! Load {0} and {1} into the CheckList? [Y/n]", strContainerFile, strRecordsFile);

                //ConsoleKeyInfo ki = Console.ReadKey();

                //if (!(ki.Key == ConsoleKey.Enter || ki.Key == ConsoleKey.Y))
                //{
                //    return;
                //}

                // make these the target per-annum loadfiles for all years in the batch

                CreateStagingFiles(strRecordsFile, strRPRecordHeaders);
                CreateStagingFiles(strContainerFile, strRPFileHeaders);
            }
            
            long i = 0;

            foreach (ContainerRec rec in ContainersDict.Values)
            {
                Console.WriteLine(String.Format("{0}: Doing Container Record {1} ...", ++i, rec.Number));
                DumpContainerRec(rec, ContainerBuckets[DateTime.Parse(rec.DateCreated).Year.ToString()]);
            }
            
            foreach (StreamWriter writer in ContainerBuckets.Values)
            {
                writer.Close();
            }


            i = 0;
            foreach (RecordRec rec in RecordsDict.Values)
            {
                Console.WriteLine(String.Format("{0}: Doing Document Record {1} ...", ++i, rec.Number));
                DumpRecordRec(rec, RecordBuckets[DateTime.Parse(rec.DateCreated).Year.ToString()]);

            }

            foreach (StreamWriter writer in RecordBuckets.Values)
            {
                writer.Close();
            }

        }

        public static string GetQuarter(DateTime inputDate)
        {
            if (inputDate.Month <= 3)
            {
                return String.Format("Jan-Mar {0}", inputDate.Year);
            }
            if (inputDate.Month <= 6)
            {
                return String.Format("Apr-Jun {0}", inputDate.Year);
            }
            if (inputDate.Month <= 9)
            {
                return String.Format("Jul-Sep {0}", inputDate.Year);
            }
            return String.Format("Oct-Dec {0}", inputDate.Year);
        }

        private static RecordRec PrefixQuarter(RecordRec rec)
        {
            rec.DestinationPath = String.Format("{0}{1}", GetQuarter(DateTime.Parse(rec.DateCreated)), 
                rec.DestinationPath.Remove(0,rec.DestinationPath.IndexOf("\\")));
            return rec;
        }

        private static ContainerRec PrefixQuarter(ContainerRec rec)
        {
            rec.ContainerHierarchy = String.Format("{0}{1}", GetQuarter(DateTime.Parse(rec.DateCreated)),
                rec.ContainerHierarchy.Remove(0, rec.ContainerHierarchy.IndexOf("\\")));
            return rec;
        }
        private long LoadCSVRecords(long lRecordCount, string strLocalRecordsFile)
        {
            if (File.Exists(strLocalRecordsFile))
            {
                string basePath = Path.GetDirectoryName(strLocalRecordsFile);
                try
                {
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        MissingFieldFound = null, // Ignore missing fields
                        BadDataFound = context => Console.WriteLine($"Bad data: {context.RawRecord}")
                    };

                    var reader = new StreamReader(strLocalRecordsFile);
                    var csv = new CsvReader(reader, config);

                    Console.WriteLine($"Loading Records ... ");
                    var records = csv.GetRecords<RecordRec>();
 
                    foreach (var rec in records)
                    {
                        lRecordCount++;
                        Console.WriteLine($"Uri: {rec.Uri}, Number: {rec.Number}");

                        DateTime dt = DateTime.Parse(rec.DateCreated);
                        if (!RecordBuckets.ContainsKey(dt.Year.ToString()))
                        {
                            RecordBuckets.Add(dt.Year.ToString(), new StreamWriter($"{basePath}\\{dt.Year}-Records.csv"));
                            RecordBuckets[dt.Year.ToString()].AutoFlush = true;
                            RecordBuckets[dt.Year.ToString()].WriteLine(strRPRecordHeaders);
                        }

                        RecordsDict.Add(rec.Uri, PrefixQuarter(rec));
                    }

                    reader.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading CSV: {ex.Message}");
                }

            }

            return lRecordCount;
        }

        private void CreateStagingFiles(string strSrcFile, string Headers)
        {
            //var reader = new StreamReader(strSrcFile);
            ////var writer = new StreamWriter(strLocalDestFile);

            //writer.WriteLine(Headers);
            //string x = reader.ReadLine();

            //while (!reader.EndOfStream)
            //{
            //    writer.WriteLine(reader.ReadLine());
            //}
            //reader.Close();
            //writer.Close();
        }
    } // class CheckList
}
