using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HP.HPTRIM.SDK;

namespace TRIMRevisionExtract
{
    internal partial class IA_Tools
    {
        public string IA_Options_DoTrimWalk = string.Empty;
        public string IA_ExtractACLs = string.Empty;
        public string IA_ExtractRecordTypes = string.Empty;
        public string IA_ExtractClassifications = string.Empty; 
        public string IA_ExtractSecurity = string.Empty;
        public string IA_ExtractDocStores = string.Empty;
        public string IA_DoRawCount = string.Empty;
        public string IA_DumpAuditHistory = string.Empty;
        public string IA_DoRevisionHistogram = string.Empty;
        public string IA_OutputFile = string.Empty;
        public string strSavedSearch = string.Empty;


        Database db = null;
        StreamWriter writer = null;
        string basePathToFiles = string.Empty;

        public IA_Tools(Database db, string path)
        {
            this.db = db;
            basePathToFiles = path;
            SetWriter("Startup.txt");
        }

        public IA_Tools(string basePathToFiles)
        {
            this.basePathToFiles = basePathToFiles;
            SetWriter("Startup.txt");
        }

        ~IA_Tools()
        {
           // writer?.Close();
           // writer?.Dispose();
        }

        public void SetDatabase(Database db)
        {
            this.db = db;
        }
        public StreamWriter SetWriter(string filename)
        {
            writer?.Close();

            writer = new StreamWriter(basePathToFiles + "\\" + filename);  
            writer.AutoFlush = true;
            return writer;
        }
        public StreamWriter Writer
        {
            get { return writer; }
            set { writer = value; }
        }

        string nonDocTypes = string.Empty;
        StringBuilder RecurseIA(Record theRecord, string node_label)
        {
            StringBuilder sb = new StringBuilder();

            TrimMainObjectSearch srch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
            srch.SelectThoseWithin(theRecord);
            long count = srch.Count;

            string nonDocFilter = "type:[" + nonDocTypes + "]";
            srch.SetFilterString(nonDocFilter); 
            //srch.SetFilterString("not behaviour:Document"); // This needs to be automatically determined

            bool leaf_node = true;

            foreach (Record rec in srch)
            {
                if (rec.IsContainer)
                {
                    TrimMainObjectSearch docSrch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
                    docSrch.SelectThoseWithin(rec);

                    string DocFilter = "not type:[" + nonDocTypes + "]";
                    docSrch.SetFilterString(DocFilter);
                    long docCount = docSrch.Count;

                    string node = node_label + rec.Number + "{" + rec.RecordType.Name + "} [" + docCount + "]" + " > ";
                    leaf_node = false;
                    RecurseIA(rec, node);
                }
            }

            if (leaf_node)
            {
                long revBytes = 0;
                srch.SetFilterString("revision>1");
                foreach (Record rec in srch)
                {
                    foreach (RecordRevision rev in rec.ChildRevisions)
                    {
                        revBytes += rev.Bytes;
                    }
                }
                writer.WriteLine(node_label + "[" + count + ":(" + srch.Count + ":" + revBytes + ")]");
            }
            return sb;
        }

        public void DoTRIMWalk()
        {
            TrimMainObjectSearch objSrch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
            objSrch.SelectTopLevels();

            SetWriter("DoTrimWalk.txt");
            Console.WriteLine("Writing DoTrimWalk.txt");

            foreach (Record rec in objSrch)
            {
                string node = rec.Number + "{" + rec.RecordType.Name + "} > ";
                RecurseIA(rec, node);
            }
        }

        public void DoRawCount()
        {
            TrimMainObjectSearch objSrch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
            objSrch.SelectAll();

            SetWriter("RawCount.txt");
            Console.WriteLine("Writing RawCount.txt");

            int revsum = 0;
            foreach (Record rec in objSrch)
            {
                revsum += rec.RevisionCount;
                if (revsum % 1000 == 0)
                {
                    Console.WriteLine("revsum: {0}", revsum);
                }

                //if (rec.RevisionCount > 0)
                //{
                //    int count = 0;
                //    foreach (RecordRevision rev in rec.ChildRevisions)
                //    {

                //    }

                //}
            }
            writer.WriteLine("Total Documents+revisions: {0}", revsum);
            Console.WriteLine("Total Documents+revisions: {0}", revsum);
        }

        string ExtractRecordTypes()
        {
            SortedDictionary<int, string> recTypes = new SortedDictionary<int, string>();

            TrimMainObjectSearch objSearch = new TrimMainObjectSearch(db, BaseObjectTypes.RecordType);
            objSearch.SelectAll();

            SetWriter("ExtractRecordTypes.txt");
            Console.WriteLine("Writing ExtractRecordTypes.txt");

            writer.WriteLine("Database({0}): RecordTypes:", db.Id);
            foreach (RecordType rt in objSearch)
            {
                StringBuilder sb = new StringBuilder();
                TrimMainObjectSearch rtSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
                rtSearch.SetSearchString("type:" + rt.Uri);

                sb.AppendFormat("{0},{1},{2},{3},{4}", rt.Name, rt.ActiveDateRangeDescription, rt.Level, rt.URN, rtSearch.Count);

                int key = Int32.Parse(rt.URN.Split('/')[2]);

                if (rt.UsualBehaviour != RecordBehaviour.Document)
                {
                    recTypes.Add(key, sb.ToString());
                }
                writer.WriteLine(sb.ToString());

                //Console.WriteLine("{0},{1},{2},{3}", rt.Name, rt.ActiveDateRangeDescription, rt.Level, rt.URN);
            }

            StringBuilder folderKeys = new StringBuilder();
            int iC = recTypes.Count-1;
            foreach (int i in recTypes.Keys.Reverse())
            {
                folderKeys.AppendFormat("{0}{1}",i.ToString(),iC-- > 0 ? "," : "");
            }

            string rVal = folderKeys.ToString();
            return rVal;
        }

        public void ExtractClassifications()
        {
            TrimMainObjectSearch objSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Classification);
            objSearch.SelectAll();
            //writer.WriteLine("Database({0}): Classifications:", db.Id);

            SetWriter("ExtractCLassifications.txt");
            Console.WriteLine("Writing ExtractCLassifications.txt");

            writer.WriteLine("Database({0}): Classifications:", db.Id);

            SortedSet<string> sl = new SortedSet<string>();
            foreach (Classification rc in objSearch)
            {
                TrimMainObjectSearch rtSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
                rtSearch.SetSearchString("classification:" + rc.Uri);

                sl.Add(rc.NameString + "," + rtSearch.Count);
            }

            TrimMainObjectSearch rcNullSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
            rcNullSearch.SetSearchString("classification:null");
            sl.Add("Unclassified," + rcNullSearch.Count);

            foreach (string c in sl)
            {
                //writer.WriteLine(c);
                writer.WriteLine(c);
            }

        }
        string CalculateMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        class RefTable
        {
            string pathToFiles = string.Empty;
            Hashtable hashtable = new Hashtable();
            public RefTable(string path)
            {
                pathToFiles = path;
            }

            public void Add(string hash, string file)
            {
                if (!hashtable.ContainsKey(hash))
                {
                    hashtable[hash] = new FileNode(file, hash);
                }
                else
                {
                    ((FileNode)hashtable[hash]).Add(file);
                }
            }

            public void Dump(string outFile)
            {
                string path = pathToFiles + "\\" + outFile;
                StreamWriter sw = new StreamWriter(path);

                foreach (var k in hashtable.Keys)
                {
                    ((FileNode)hashtable[k]).Dump(sw);
                }
                sw.Close();

                sw.Dispose();
            }
            class FileNode
            {
                List<string> files = null;
                string hashKey = string.Empty;
                int count = 0;
                public FileNode(string path, string hash)
                {
                    files = new List<string>();
                    files.Add(path);
                    hashKey = hash;
                    count++;
                }

                public int Add(string path)
                {
                    files.Add(path);
                    count++;
                    return files.Count;
                }

                public void Dump(StreamWriter outS)
                {
                    foreach (string path in files)
                    {
                        outS.WriteLine("{0} {1}", hashKey, path);
                    }
                }

                public void Load(StreamReader inS)
                {
                    string[] line = inS.ReadLine().Split(' ');
                    string hashKey = line[0];
                    while (line[0] == hashKey)
                    {

                        line = inS.ReadLine().Split(' ');
                    }
                }
            };
        };
        public void ExtractACLs(string strExtractBaseDir)
        {
            //TrimSecurityProfile tsp = new TrimSecurityProfile(db);
            //Console.WriteLine(tsp.ToString());

            RefTable refTable = new RefTable(strExtractBaseDir);

            TrimMainObjectSearch objSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
            objSearch.SelectAll();

            SetWriter("ExtractACLs.txt");

            writer.WriteLine("Database({0}): Notes: ACLS to {1}", db.Id, strExtractBaseDir);
            Console.WriteLine("Writing ExtractACLs.txt");

            foreach (Record rt in objSearch)
            {
                string outF = strExtractBaseDir + "\\" + rt.Name.Replace('/', '-') + "-ACL.txt";
                StreamWriter swRecordsOutfile = new StreamWriter(outF);

                swRecordsOutfile.WriteLine("{0}:{1}", rt.Number, rt.Title);

                swRecordsOutfile.WriteLine("ViewRecord:" + rt.AccessControlList.GetAsString(((int)RecordAccess.ViewRecord)));
                swRecordsOutfile.WriteLine("ViewDocument:" + rt.AccessControlList.GetAsString(((int)RecordAccess.ViewDocument)));
                swRecordsOutfile.WriteLine("UpdateMetadata:" + rt.AccessControlList.GetAsString(((int)RecordAccess.UpdateMetadata)));
                swRecordsOutfile.WriteLine("UpdateDocument:" + rt.AccessControlList.GetAsString(((int)RecordAccess.UpdateDocument)));
                swRecordsOutfile.WriteLine("ModifyAccess:" + rt.AccessControlList.GetAsString(((int)RecordAccess.ModifyAccess)));
                swRecordsOutfile.WriteLine("DestroyRecord:" + rt.AccessControlList.GetAsString(((int)RecordAccess.DestroyRecord)));
                swRecordsOutfile.WriteLine("AddContents:" + rt.AccessControlList.GetAsString(((int)RecordAccess.AddContents)));
                swRecordsOutfile.WriteLine("AccessExclusions:" + rt.AccessExclusions.ToString());
                swRecordsOutfile.WriteLine("Security:" + rt.Security.ToString());

                swRecordsOutfile.Close();

                string hash = CalculateMD5(outF);
                refTable.Add(hash, outF); // add this instance to the hashTable - if it's a collision, just increment an instance counter? 
                //swRecordsOutfile.Dispose();

            }
            refTable.Dump("TestHashDump.txt");
        }

        public void ExtractSecurity(string strExtractBaseDir)
        {
            //Security
            //
            TrimMainObjectSearch objSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
            objSearch.SelectAll();

            SetWriter("ExtractSecurity.txt");
            Console.WriteLine("Writing ExtractSecurity.txt");

            writer.WriteLine("Database({0}): Notes: ACLS to {1}", db.Id, strExtractBaseDir);

            foreach (Record rt in objSearch)
            {
                //string outF = strExtractBaseDir + "\\" + rt.Name.Replace('/', '-') + "-Notes.txt";
                //swRecordsOutfile = new StreamWriter(outF);

                writer.WriteLine("{0}: Security:[{1},{2}]", rt.Name, rt.Security, rt.SecurityLocks);

                //swRecordsOutfile.WriteLine(rt.Notes);
                //swRecordsOutfile.Close();
            }
        }

        public void ExtractDocStoreData()
        {
            TrimMainObjectSearch objSearch = new TrimMainObjectSearch(db, BaseObjectTypes.ElectronicStore);
            objSearch.SelectAll();

            SetWriter("ExtractDocStoreData.txt");
            Console.WriteLine("Writing ExtractDocStoreData.txt");

            foreach (ElectronicStore es in objSearch)
            {
                writer.WriteLine("Name: {0}, Path: {1}, Size Used/Remaining: {2}/{3}, StoreLocation: {4}, StoreType: {5}", 
                    es.Name, es.Path, es.BytesUsed, es.BytesRemaining, es.StoreLocation, es.StoreType);
            }

        }

        // Class to return a histogram of numbers of revisions in the documents implicated by a saved search
        class DocHistogram
        {
            string outputFile = string.Empty;
            Hashtable hashtable = new Hashtable();

            public DocHistogram(string outFile)
            {
                outputFile = outFile;
            }

            public void Add(int hash, ulong fileSize)
            {
                if (!hashtable.ContainsKey(hash))
                {
                    hashtable[hash] = new RevisionNode(hash, fileSize);
                }
                else
                {
                    ((RevisionNode) hashtable[hash]).Add(fileSize);
                }
            }

            public void Dump()
            {
                StreamWriter streamWriter = new StreamWriter(outputFile);

                int[] sortedKeys = new int[hashtable.Count];
                hashtable.Keys.CopyTo(sortedKeys, 0);
                Array.Sort(sortedKeys);

                streamWriter.WriteLine("revCount,Instances,Average Size,Low Water Mark,High Water Mark");

                //foreach (RevisionNode node in hashtable.Values.)

                foreach (int key in sortedKeys)
                {
                        ((RevisionNode)hashtable[key]).Dump(streamWriter);
                }

                streamWriter.Close();
            }

            class RevisionNode
            {
                public int hash;
                public int numRevisions = 0;
                public ulong low_water_mark = ulong.MaxValue;
                public ulong high_water_mark = ulong.MinValue;
                ulong accumulator = 0;
                public RevisionNode(int hash, ulong fileSize)
                {
                    this.hash = hash;
                    Add(fileSize);
                }

                public void Add(ulong fileSize)
                {
                    numRevisions++;
                    accumulator += (ulong)fileSize;

                    if (fileSize < low_water_mark)
                    {
                        low_water_mark = fileSize;
                    }
                    if (fileSize > high_water_mark)
                    {
                        high_water_mark = fileSize;
                    }

                }

                public void Dump(StreamWriter output)
                {
                    ulong average = (ulong)accumulator / (ulong)numRevisions;
                    output.WriteLine("{0},{1},{2},{3},{4}", hash, numRevisions, average, low_water_mark, high_water_mark);
                }
            }

        }

        void GetRevisionDataRecursive(Record objRec)
        {
            if (objRec == null)
            {
                return;
            }

            if (objRec.IsElectronic)
            {
                if (recCount++ % 1000 == 0)
                {
                    Console.WriteLine("eRecords processed: {0}", recCount);
                }

                dh.Add(objRec.RevisionCount, (ulong)objRec.DocumentSize);
            }
            else if (!objRec.IsContainer)
            {
                dh.Add(0, (ulong)0);
                recCount++;
            }

            if (objRec.AlternativeContainers != string.Empty)
            {
                Writer.WriteLine("{0},\"{1}\",\"{2}\"", objRec.Number, objRec.AlternativeContainers.Replace("\r\n", ";"), objRec.RelatedRecs.Replace("\r\n", ";"));
            }

            if (objRec.IsContainer)
            {
                TrimMainObjectSearch objSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
                //objSearch.DefaultIncludesContent = false;
                string strSearchClause = string.Format("container:[number:{0}]", objRec.Number);
                objSearch.SetSearchString(strSearchClause);

                foreach (Record rec in objSearch)
                {
                    GetRevisionDataRecursive(rec);
                }
            }

        }


        /// <summary>
        /// DumpRevisionHistogram - trawl TRIM from Saved Search and collect a histogram of revisions and instance count
        /// </summary>

        long recCount = 0;
        DocHistogram dh = null;
        void DumpRevisionHistogram()
        {
            
            dh = new DocHistogram(IA_OutputFile);

            SetWriter("IA-AltContainers.csv");
            Writer.WriteLine("Number,Alternate Containers,Related Records");

            // do walkies here
            SavedSearch ss = new SavedSearch(db, strSavedSearch);
            TrimMainObjectSearch objSearch = ss.GetSearch();

            foreach (Record objRec in objSearch)
            {
                GetRevisionDataRecursive(objRec);
            }

            dh.Dump();
            Console.WriteLine("eRecords processed: {0}", recCount);

        }

        Stopwatch _sw = null;
        public void DoTrimIAStuff(Stopwatch sw)
        {
            _sw = sw;
            //ACLSR

            if (IA_DoRevisionHistogram == "true")
            {
                DumpRevisionHistogram();
            }

            if (IA_DumpAuditHistory == "true")
            {
                DumpAuditHistory();
            }
            //
            if (IA_ExtractACLs == "true")
            {
                ExtractACLs(basePathToFiles);
            }

            if (IA_ExtractRecordTypes == "true")
            {
                nonDocTypes = ExtractRecordTypes();
            }

            if (IA_Options_DoTrimWalk == "true")
            {
                DoTRIMWalk();
            }

            if (IA_ExtractClassifications == "true")
            {
                ExtractClassifications();
            }

            //Caveats
            //
            if (IA_ExtractSecurity == "true")
            { 
                ExtractSecurity(basePathToFiles);
            }

            if (IA_ExtractDocStores == "true")
            {
                ExtractDocStoreData();
            }

            if (IA_DoRawCount == "true")
            {
                DoRawCount();
            }
        }

        // Notes
        //

        void ExtractNotes(StreamWriter swRecordsOutfile, string strExtractBaseDir)
        {
            TrimMainObjectSearch objSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
            objSearch.SelectAll();
            Console.WriteLine("Database({0}): Notes: writing to {1}", db.Id, strExtractBaseDir);

            foreach (Record rt in objSearch)
            {
                string outF = strExtractBaseDir + "\\" + rt.Name.Replace('/', '-') + "-Notes.txt";
                swRecordsOutfile = new StreamWriter(outF);

                if (rt.Notes.Length > 0)
                {
                    swRecordsOutfile.WriteLine(rt.Notes);
                }

                if (rt.History != string.Empty)
                {
                    Console.WriteLine(rt.History);
                }
                swRecordsOutfile.Close();

            }

        } // ExtractNotes


        StringBuilder RecurseBranch(string recNum)
        {
            StringBuilder sb = new StringBuilder();
            TrimMainObjectSearch objSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);

            string src = "container:[" + recNum + "]";
            objSearch.SetSearchString(src);
            //objSearch.SetFilterString("type:" + s);
            objSearch.SelectAll();

            Console.WriteLine("Count:{0}", objSearch.Count);
            if (objSearch.Count != 0)
            {
                foreach (Record rec in objSearch)
                {
                    RecurseBranch(rec.Number);
                }
            }
            return sb;
        }

        public StringBuilder RecurseIA(Stack<string> recTypes)
        {
            StringBuilder sb = new StringBuilder();
            string s = String.Empty;

            s = recTypes.Pop();

            if (recTypes.Count > 0)
            {
                RecurseIA(recTypes);
            }

            TrimMainObjectSearch objSearch = new TrimMainObjectSearch(db, BaseObjectTypes.Record);
            objSearch.SetSearchString("all");
            objSearch.SetFilterString("type:" + s);
            objSearch.SelectAll();

            Console.WriteLine("Type:{1}, Count:{0}", objSearch.Count, s);

            foreach (Record rec in objSearch)
            {
                RecurseBranch(rec.Uri.ToString());
            }

            return sb;
        }

        public void DumpAuditHistory()
        {
            SetWriter("AuditHistoryDump.csv");
            TrimMainObjectSearch srch = new TrimMainObjectSearch(db, BaseObjectTypes.History);
            srch.SelectAll();
            Console.WriteLine("There are {0} audit events in this dump.", srch.Count);

            writer.WriteLine("EventNumber, ForObjectUri, EventDescription");
            long i = 0;
            foreach (History h in srch)
            {
                string hs = h.EventDescription;
                Writer.WriteLine("{0}, {1}, {2}", i++, h.ForObjectUri.ToString(), hs);

                if(i%1000==0)
                {
                    Console.WriteLine("Elapsed Time={0}, Record Count={1}", _sw.Elapsed, i);
                }
            }
            // srch.SetFilterString("number:" + rec.Number);
            //srch.SelectByUris(x);

            long count = srch.Count;

        }

    }
}
