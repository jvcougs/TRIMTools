using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HP.HPTRIM.SDK;

namespace TRIMRevisionExtract
{
    internal partial class Program
    {
        static Dictionary<string, string> eStoreMap = new Dictionary<string, string>();
        public static void LoadEStoreMap()
        {
            int numStores = Int32.Parse(ConfigTools.GetAppSetting("NumberOfDocStores"));

            for (int i=0; i<numStores; i++)
            {
                string eStoreSrcStr = ConfigTools.GetAppSetting("DocStoreSrc_" + i.ToString());
                string eStoreDestStr = ConfigTools.GetAppSetting("DocStoreDst_" + i.ToString());

                eStoreMap.Add(eStoreSrcStr, eStoreDestStr);
            }
        }

        class revData : IEquatable<revData> 
        {
            public string SourcePath;
            public string DestinationPath;
            public string Version;

            public revData(string sourcePath, string destinationPath, string version)
            {
                SourcePath = sourcePath;
                DestinationPath = destinationPath;
                Version = version;
            }

            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                revData objAsrevData = obj as revData;
                if (objAsrevData == null) return false;
                else  return Equals(objAsrevData);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public bool Equals(revData other)
            {
                if (other == null) return false;
                return (this.Version == other.Version);
            }
            public string Dump()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("{0},{1},{2}", SourcePath, DestinationPath, Version);
                return sb.ToString();
            }

            public void Dump(ref Dictionary<string, string> cache)
            {
                cache["SourcePath"] = SourcePath;
                cache["DestinationPath"] = DestinationPath;
                cache["Version"] = Version;
            }
        }

        /// <summary>
        /// GetDocumentStack: get the base document and all the revision documents for this record in one fell swoop.
        /// </summary>
        /// <param name="objRec"></param>
        /// <returns></returns>
        private static List<revData> GetDocumentStack(Record objRec)
        {
            if (lstContainerExclusionList.BinarySearch(objRec.Number) >= 0)
            {
                swPerfAndLogOutfile.WriteLine("GetDocumentStack: Excluding record number: {0}", objRec.Number);
                Console.WriteLine("GetDocumentStack: Excluding record number: {0}", objRec.Number);
                return null;
            }

            // get a modified name for the document stack base to use to write out to the filesystem
            string SPPath = objRec.ContainerHierarchy.Replace('/', '-').Replace(" > ", "\\");
            int sIndex = objRec.EStoreId.IndexOf('.');

            if (strDebug == "debug")
            {
                Console.WriteLine("GetDocumentStack({0}): sIndex=[{1}] eStoreId=[{2}]", 1, sIndex, objRec.EStoreId);
            }

            if (objRec.EStoreId.Contains("TRANSFER_"))
            {
                // your docs have transfer lock problems
                Console.WriteLine("GetDocumentStack: encountered a document store transfer lock for record number {0}. Abandoning this stack.", objRec.Number);
                swPerfAndLogOutfile.WriteLine("GetDocumentStack: encountered a document store transfer lock for record number {0}. Abandoning this stack.", objRec.Number);
                return null;
            }

            if (sIndex < 0)
            {
                swPerfAndLogOutfile.WriteLine("GetDocumentStack: We shouldn't be here!: {0}", objRec.Number);
                Console.WriteLine("GetDocumentStack: We shouldn't be here!: {0}", objRec.Number);
                int wtf = lstContainerExclusionList.BinarySearch(objRec.Number);
                Console.WriteLine("GetDocumentStack: lstContainerExclusionList.BinarySearch({0}) returns {1}", objRec.Number, wtf);

                return null;
            }

            List<revData> revStack = new List<revData>();

            string src_basename = SPPath + "\\" + objRec.EStoreId.Remove(sIndex) + "-" + objRec.SuggestedFileName.Replace(',', ' ').Replace('"', ' ');
            
            string uniquifier = objRec.EStoreId.Remove(0, objRec.EStoreId.LastIndexOf('+')+1);
            uniquifier = uniquifier.Remove(uniquifier.IndexOf('.'));

            string extension = "." + objRec.Extension;
            string fName = objRec.SuggestedFileName.Replace(',', ' ').Replace('"', ' ').Replace(extension,"");
            string dst_basename = SPPath + "\\" + fName + "-" + uniquifier + extension;

            // need to sort out the path mapping between differently placed EStore clones here ...
            string SourcePath = string.Empty;

            if (strDebug == "debug")
            {
                Console.WriteLine("GetDocumentStack({0}), before TrimExtractDocuments test", 2);
            }

            if (TrimExtractDocuments == "true")
            {
                string subPath = strExtractBaseDir + "\\" + src_basename;

                if (subPath.Length > 248)
                {
                    subPath = subPath.Remove(248);
                }
                SourcePath = subPath + "-" + objRec.RevisionNumber + "." + objRec.Extension;
            }
            else
            {
                // need to replace the initial docstore path upto the DBId with a mapped location, and perhaps more than one
                //SourcePath = eStorePath.Replace();

                //string eStorePath = eStoreMap[objRec.EStore.Path] + objRec.EStoreId.Replace('+', '\\');
                string eStorePath = string.Empty;

                if (TrimExtractUseDocStoreMap == "true")
                {
                    eStorePath = eStoreMap[objRec.EStore.Path] + objRec.EStoreId.Replace('+', '\\');
                } 
                else
                {
                    eStorePath = objRec.EStore.Path + objRec.EStoreId.Replace('+', '\\');
                }

                if (!File.Exists(eStorePath))
                {
                    swPerfAndLogOutfile.WriteLine("GetDocumentStack: eStorePath not found: [{0}]", eStorePath);
                }

                SourcePath = eStorePath;
            }

            if (strDebug == "true")
            {
                swPerfAndLogOutfile.WriteLine("GetDocumentStack: SourcePath: [{0}]", SourcePath);
            }

            revStack.Add(new revData(SourcePath, dst_basename, objRec.RevisionNumber.ToString()));
            // Store to extract staging directory here .. 

            try
            {
                if (TrimExtractDocuments == "true")
                {
                    try
                    {
                        objRec.GetDocument(SourcePath, false, "", "");
                    }
                    catch (Exception ex)
                    {
                        swPerfAndLogOutfile.WriteLine("GetDocumentStack: eStorePath not found: [{0}]", SourcePath);
                        swPerfAndLogOutfile.WriteLine("GetDocumentStack: ex={0}", ex.Message);
                    }
                }

                foreach (RecordRevision rev in objRec.ChildRevisions)
                {
                    // Store to temp directory here .. 
                    if (TrimExtractDocuments == "true")
                    {
                        src_basename = SPPath + "\\" + rev.EStoreId.Remove(rev.EStoreId.IndexOf('.')) + "-" + objRec.SuggestedFileName.Replace(',', ' ').Replace('"', ' ');
                        string subPath = strExtractBaseDir + "\\" + src_basename;


                        if (subPath.Length > 248)
                        {
                            subPath = subPath.Remove(248);
                        }
                        SourcePath = subPath + "-" + rev.RevisionNumber + "." + rev.Extension;

                        try
                        {
                            rev.Extract(SourcePath, false);
                        }
                        catch (Exception ex)
                        {
                            swPerfAndLogOutfile.WriteLine("GetDocumentStack: eStorePath not found: [{0}]", SourcePath);
                            swPerfAndLogOutfile.WriteLine("GetDocumentStack: ex={0}", ex.Message);
                        }
                    }
                    else
                    {
                        string eStorePath = string.Empty;
                        if (TrimExtractUseDocStoreMap == "true")
                        {
                            eStorePath = eStoreMap[rev.EStore.Path] + rev.EStoreId.Replace('+', '\\');
                        }
                        else
                        {
                            eStorePath = rev.EStore.Path + rev.EStoreId.Replace('+', '\\');
                        }

                        if (!File.Exists(eStorePath))
                        {
                            swPerfAndLogOutfile.WriteLine("GetDocumentStack: eStorePath not found: [{0}]", eStorePath);
                        }

                        SourcePath = eStorePath;
                    }

                    revData x = new revData(SourcePath, dst_basename, rev.RevisionNumber.ToString());

                    if (!revStack.Contains(x))
                    {
                        revStack.Add(x);
                    }
                    else
                    {
                        Console.WriteLine("GetDocumentStack: Duplicate entry in revision stack: {0}, {1}, {2}", x.SourcePath, x.DestinationPath, x.Version);
                        swPerfAndLogOutfile.WriteLine("GetDocumentStack: Duplicate entry in revision stack: {0}, {1}, {2}", x.SourcePath, x.DestinationPath, x.Version);
                    }
                } // foreach
            }
            catch (Exception e) 
            { 
                swPerfAndLogOutfile.WriteLine("GetDocumentStack: Exception caught: {0}", e); 
            }

            return revStack;
        }


    }
}
