using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using HP.HPTRIM.SDK;

namespace TRIMRevisionExtract
{
    internal partial class Program
    {
        static Database _db = null;
        public static void DoTrimStuff(Database db)
        {

            PrePopulateFieldCache(db);

            // Connect to Saved Search here .. 
            SavedSearch ss = new SavedSearch(db, strSavedSearch);
            TrimMainObjectSearch objSearch = ss.GetSearch();
            objSearch.DefaultIncludesContent = false;

            _db = db;

            long numObjects = objSearch.Count;
            //float recs_per_sec = 2.6F;
            long estimate = numObjects / 160;

            Console.WriteLine("DoTrimStuff: retrieving [{0}]", objSearch.SearchString);
            swPerfAndLogOutfile.WriteLine("DoTrimStuff: retrieving [{0}]", objSearch.SearchString);

            if (File.Exists(strCheckListFile))
            {
                Console.WriteLine("Appending to {0}", strRecordsOutfile);
                swPerfAndLogOutfile.WriteLine("Appending to {0}", strRecordsOutfile);
 
                swCheckListFile = new StreamWriter(strCheckListFile, append: true);
            }
            else
            {
                Console.WriteLine("Writing to {0}", strRecordsOutfile);
                swPerfAndLogOutfile.WriteLine("Writing to {0}", strRecordsOutfile);

                swCheckListFile = new StreamWriter(strCheckListFile);
            }

            swCheckListFile.AutoFlush = true;

            swRecordsOutfile = new StreamWriter(strRecordsOutfile);
            swRecordsOutfile.AutoFlush = true;
            swRecordsOutfile.WriteLine(string.Join(",", recordHeaderTable.Values));

            Console.WriteLine("Writing to {0}", strContainerOutfile);
            swPerfAndLogOutfile.WriteLine("Writing to {0}", strContainerOutfile);

            swContainerOutfile = new StreamWriter(strContainerOutfile);
            swContainerOutfile.AutoFlush = true;
            swContainerOutfile.WriteLine(string.Join(",", fileHeaderTable.Values));
 
            foreach (Record objRec in objSearch)
            {
                GetRecordsAndPayloadRecursive(objRec);
            }

            if (objSearch != null)
            {
                // dispose??
            }

            swRecordsOutfile.Close();
            swContainerOutfile.Close();
            swCheckListFile.Close();

            Console.WriteLine("DoTrimStuff: retrieved [{0}]", objSearch.SearchString);
            swPerfAndLogOutfile.WriteLine("DoTrimStuff: retrieved [{0}]", objSearch.SearchString);
        }

        private static Dictionary<long, List<long>> _recordTypeUserFieldUriCache = new Dictionary<long, List<long>>();
        //        private readonly ConcurrentDictionary<long, List<int>> _recordTypeFieldsCache = new ConcurrentDictionary<long, List<int>>();

        private static void PrePopulateFieldCache(Database db)
        {
            Console.WriteLine("Pre-indexing Record Type User-Defined Fields...");

            TrimMainObjectSearch rtSearch = new TrimMainObjectSearch(db, BaseObjectTypes.RecordType);
            rtSearch.SelectAll();

            foreach (RecordType rt in rtSearch)
            {
                var userFieldUris = new List<long>();

                // The correct property on RecordType for Additional/User Fields is UserFields
                FieldDefinitionList uniqueUserFields = rt.UserFields;

                if (uniqueUserFields != null)
                {
                    foreach (FieldDefinition fieldDef in uniqueUserFields)
                    {
                        // Cache the unique long URI identifier for the user defined field
                        userFieldUris.Add(fieldDef.Uri);
                    }
                }

                _recordTypeUserFieldUriCache[rt.Uri] = userFieldUris;
            }
        }

        static void GetRecordProperties(Record rec)
        {
            PropertyInfo[] properties = typeof(Record).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                string name = property.Name;
                Type type = property.PropertyType;
                string value = string.Empty;

                if (strDebug == "Debug2")
                {
                    swPerfAndLogOutfile.WriteLine("GetRecordProperties: name={0}, type.Name={1}", name, type.Name);
                }

                // bug: if you try this value, it hangs ...
                if (name == "AggregatedDisposal") { continue; }

                string theValue = String.Empty;

                try
                {
                    var p = property.GetValue(rec);

                    if (p != null)
                    {
                        switch (type.Name)
                        {
                            case "Record": value = ((Record)p).Name.ToString(); break;
                            case "Location": value = ((Location)p).Name.ToString(); break;
                            case "Schedule": value = ((Schedule)p).Name.ToString(); break;
                            case "RecordType": value = ((RecordType)p).Name.ToString(); break;
                            case "ElectronicStore": value = ((ElectronicStore)p).Name.ToString(); break;

                            //case "TrimAccessControlList": value = ((TrimAccessControlList)p).Name.ToString(); break;

                            default: break;
                        }

                        // value massaging section ...

                        theValue = (string)(value == string.Empty ? p.ToString() : value);

                        // sanitisation ... ffs - specific munging rules to get out of problems ...
                        if (type.Name == "TrimDateTime" && theValue == "1/1/0001 12:00:00 AM")
                        {
                            theValue = string.Empty;
                        }
                    } // p != null
                }
                catch (Exception ex)
                {
                    Console.WriteLine("GetRecordProperties: Exception: {0}, property name=[{1}]", ex.Message, name);
                    swPerfAndLogOutfile.WriteLine("GetRecordProperties: Exception: {0}, property name=[{1}]", ex.Message, name);

                    theValue = string.Empty;
                }

                if (type.Name == "Location")
                {
                    FixLocation(rec, name, ref theValue);
                }

                if (name == "History")
                {
                    GetShortHistory(rec, name, ref theValue);
                }
                else // History needs CRLF and wrapped in quotes
                {
                    theValue = SanitiseFieldValue(theValue);
                }

                if (rec.IsContainer)
                {
                    if (fileHeaderTable.ContainsKey(name))
                    {

                        fileValueTable[fileHeaderTable[name]] = theValue;
                    }
                }
                else
                {
                    if (recordHeaderTable.ContainsKey(name))
                    {
                        recordValueTable[recordHeaderTable[name]] = theValue;
                    }
                }
                //Console.WriteLine(name + ": " + (value == string.Empty ? p : value));
            } // for each property

            // for each additional field
            long recordTypeUri = rec.RecordType.Uri;
            long recui = rec.Uri;

            // Direct O(1) lookup to find only the fields relevant to this record type
            if (_recordTypeUserFieldUriCache.TryGetValue(recordTypeUri, out List<long> relevantFields))
            {
                foreach (long fieldUri in relevantFields)
                {
                    string valueString = string.Empty;
                    FieldDefinition fieldDef = new FieldDefinition(rec.Database, fieldUri);

                    UserFieldValue value = rec.GetFieldValue(fieldDef);

                    switch (fieldDef.Format)
                    {
                        case UserFieldFormats.Date:
                        case UserFieldFormats.Datetime:
                            TrimDateTime trimDate = value.AsDate();
                            valueString = (trimDate != null && !trimDate.IsClear)
                                ? trimDate.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss")
                                : null;
                            break;

                        case UserFieldFormats.Boolean:
                            valueString = value.AsBool() ? "1" : "0"; // Uniform binary representation
                            break;

                        case UserFieldFormats.Object:
                            Location linkedLoc = value.AsTrimObject() as Location;
                            valueString = linkedLoc != null ? linkedLoc.FormattedName : null; // Relational foreign key
                            break;

                        default:
                            // Safe standard handling for basic String, Text, Number, and Currency fields
                            valueString = rec.GetFieldValueAsString(fieldDef, StringDisplayType.ViewPane, false);
                            break;
                    }

                    if (strDebug == "Debug2" && !string.IsNullOrEmpty(valueString))
                    {
                        Console.WriteLine($"Record: {rec.Number} | Field: {fieldDef.Name} | Value: {valueString}");
                    }
                    // get field name and value
                    // lodge in corresponding slot in record/file value table
                    //

                    if (rec.IsContainer)
                    {
                        if (fileHeaderTable.ContainsKey(fieldDef.Name))
                        {

                            fileValueTable[fileHeaderTable[fieldDef.Name]] = valueString;
                        }
                    }
                    else
                    {
                        if (recordHeaderTable.ContainsKey(fieldDef.Name))
                        {
                            recordValueTable[recordHeaderTable[fieldDef.Name]] = valueString;
                        }
                    }
                }
            }
        }
 
        static void FixLocation(Record rec, string propName, ref string theValue)
        {
            Record container = rec.Container;

            if (strDebug == "Debug2")
            {
                Console.WriteLine("FixLocation: Container.Name:[{0}] propName={1}, theValue={2}", container.Name, propName, theValue);
            }
            switch (propName)
            {
                case "HomeLocation":
                    if (rec.HomeLocationStatus == RecLocSubTypes.InContainer)
                    {
                        theValue = container.Name + " (In container)";
                    }
                    else if (rec.HomeLocationStatus == RecLocSubTypes.AtHome)
                    {
                        string slug = container.Title;
                        if (slug.Contains("-"))
                        {
                            slug = slug.Remove(slug.IndexOf("-"));
                        }
                        theValue = "In container '" + container.Name + " (" + slug.TrimEnd(' ') + ")' since " + rec.DateAssigned;
                    }
                    else if (rec.HomeLocationStatus == RecLocSubTypes.AtLocation)
                    {
                        theValue = theValue + " since " + rec.DateAssigned; //?
                    }
                    break;
                case "Assignee":
                    if (rec.AssigneeStatus == RecLocSubTypes.InContainer)
                    {
                        theValue = rec.Container.Name + " (In container)";
                    }
                    else if (rec.AssigneeStatus == RecLocSubTypes.AtHome)
                    {
                        string slug = container.Title;
                        if (slug.Contains("-"))
                        {
                            slug = slug.Remove(slug.IndexOf("-"));
                        }
                        theValue = "In container '" + container.Name + " (" + slug.TrimEnd(' ') + ")' since " + rec.DateAssigned;
                    }
                    else if (rec.AssigneeStatus == RecLocSubTypes.AtLocation)
                    {
                        theValue = theValue + " since " + rec.DateAssigned;
                    }
                    break;
                default:
                    break;
            }
        }

        static int iCount = 0;
        public static void GetRecordsAndPayloadRecursive(Record objRec)
        {
            if (iCount++ % 1000 == 0)
            {
                Console.WriteLine("GetRecordsAndPayloadRecursive: {0} Clearing cache ...",iCount);
                swPerfAndLogOutfile.WriteLine("GetRecordsAndPayloadRecursive: {0} Clearing cache ...", iCount);
                _db.RefreshCache();
            }

            if (objRec == null)
            {
                Console.WriteLine("GetRecordsAndPayloadRecursive: null record encountered!");
                swPerfAndLogOutfile.WriteLine("GetRecordsAndPayloadRecursive: null record encountered!");
                return;
            }

            if (lstContainerExclusionList.BinarySearch(objRec.Number) >= 0)
            {
                swPerfAndLogOutfile.WriteLine("GetRecordsAndPayloadRecursive: Excluding record number: {0}", objRec.Name);
                Console.WriteLine("GetRecordsAndPayloadRecursive: Excluding record number: {0}", objRec.Name);
                return; 
            }

            GetRecordsAndPayload(objRec);

            if (objRec.IsContainer)
            {
                TrimMainObjectSearch objSearch = new TrimMainObjectSearch(_db, BaseObjectTypes.Record);
                objSearch.DefaultIncludesContent = false;
                string strSearchClause = string.Format("container:[number:{0}]", objRec.Number);
                objSearch.SetSearchString(strSearchClause);
                //objSearch.SelectAll();

                foreach (Record rec in objSearch)
                {
                    GetRecordsAndPayloadRecursive(rec);
                }
            }
        }
        static void GetRecordsAndPayload(Record objRec)
        {
            if (objRec == null)
            {
                Console.WriteLine("GetRecordsAndPayload: null record encountered!");
                swPerfAndLogOutfile.WriteLine("GetRecordsAndPayload: null record encountered!");
                return;
            }

            if (true == checkList?.doCheckNumber(objRec.Number))
            {
                Console.WriteLine("GetRecordsAndPayload: {0} checklist hit!", objRec.Number);
                swPerfAndLogOutfile.WriteLine("GetRecordsAndPayload: {0} checklist hit!", objRec.Number);
                if (!objRec.IsContainer && objRec.IsElectronic)
                {
                    gActualRecordCount++;
                }

                return;
            }

            Console.WriteLine("{0}: Doing record number: {1}", ++gRecCount, objRec.Name);
            swPerfAndLogOutfile.WriteLine("{0}: Doing record number: {1}", gRecCount, objRec.Name);
            
            swCheckListFile.WriteLine(objRec.Number);

            // get all document payload associated with this record
            if (objRec != null)
            {
                GetRecordProperties(objRec);

                if (objRec.Classification != null &&
                    objRec.Classification.Name.EndsWith("(NAP)"))
                {
                    swPerfAndLogOutfile.WriteLine("{0} {1} has classification {2}, omitting ...", objRec.Name, objRec.Number, objRec.Classification.Name);
                    Console.WriteLine("{0} {1} has classification {2}, omitting ...", objRec.Name, objRec.Number, objRec.Classification.Name);
                }
                else if (objRec.IsContainer)
                {
                    // need to improve the handling of the various naming conventions for folder numbering here ...
                    // BINGO: got bit by '/' in record numbers
                    if (fileHeaderTable.ContainsKey("ContainerHierarchy"))
                    {
                        fileValueTable[fileHeaderTable["ContainerHierarchy"]] =
                            objRec.ContainerHierarchy.Replace('/', '-').Replace(" > ", "\\") + "\\" +
                            objRec.Number.Replace('/', '-') + "\\";
                    }

                    string line = string.Join(",", fileValueTable.Values);
                    swContainerOutfile.WriteLine(line);
                    //swContainerOutfile.Flush();
                }
                else if (objRec.IsElectronic && !objRec.IsContainer)
                {
                    //cache the completed metadata line
                    List<revData> docRevisions = GetDocumentStack(objRec); // need to gather the outputfile names and perhaps format them
                    foreach (revData rev in docRevisions)
                    {
                        rev.Dump(ref recordValueTable);
                        string line = string.Join(",", recordValueTable.Values);
                        //string csvLine = line + "," + rev.Dump();

                        swRecordsOutfile.WriteLine(line);
                        swRecordsOutfile.Flush();
                    }

                    if (docRevisions.Count > 1)
                    {
                        gRecordsWithRevisions++;
                    }

                    gActualRecordCount++;
                } // likely need a case for isElectronic && isContainer
                else
                {
                    Console.WriteLine("Unknown status {0}: Number:{1} ", objRec.Name, objRec.Number);
                    swPerfAndLogOutfile.WriteLine("Unknown status {0}: Number:{1} ", objRec.Name, objRec.Number);

                    gErrorRecordCount++;
                }
            }
        }


        static void GetRecordsAndPayload(TrimMainObjectSearch objSearch)
        {

            foreach (Record objRec in objSearch)
            {
                Console.WriteLine("{0}: Doing record number: {1}", ++gRecCount, objRec.Name);
                swPerfAndLogOutfile.WriteLine("{0}: Doing record number: {1}", gRecCount, objRec.Name);

                // get all document payload associated with this record
                if (objRec != null)
                {
                    GetRecordProperties(objRec);

                    if (objRec.Classification != null &&
                        objRec.Classification.Name.EndsWith("(NAP)"))
                    {
                        swPerfAndLogOutfile.WriteLine("{0} {1} has classification {2}, omitting ...", objRec.Name, objRec.Number, objRec.Classification.Name);
                        Console.WriteLine("{0} {1} has classification {2}, omitting ...", objRec.Name, objRec.Number, objRec.Classification.Name);
                    }
                    else if (objRec.IsContainer)
                    {
                        // need to improve the handling of the various naming conventions for folder numbering here ...
                        // BINGO: got bit by '/' in record numbers
                        if (fileHeaderTable.ContainsKey("ContainerHierarchy"))
                        {
                            fileValueTable[fileHeaderTable["ContainerHierarchy"]] = 
                                objRec.ContainerHierarchy.Replace('/','-').Replace(" > ", "\\") + "\\" +
                                objRec.Number.Replace('/','-') + "\\";
                        }

                        string line = string.Join(",", fileValueTable.Values);
                        swContainerOutfile.WriteLine(line);
                        swContainerOutfile.Flush();
                    }
                    else if (objRec.IsElectronic && !objRec.IsContainer)
                    {
                        //cache the completed metadata line
                        List<revData> docRevisions = GetDocumentStack(objRec); // need to gather the outputfile names and perhaps format them
                        foreach (revData rev in docRevisions)
                        {
                            rev.Dump(ref recordValueTable);
                            string line = string.Join(",", recordValueTable.Values);
                            //string csvLine = line + "," + rev.Dump();

                            swRecordsOutfile.WriteLine(line);
                            swRecordsOutfile.Flush();
                        }
                    } // likely need a case for isElectronic && isContainer
                    else
                    {
                        Console.WriteLine("Unknown status: " + objRec.Name);
                    }
                }
            }
        }


    }
}
