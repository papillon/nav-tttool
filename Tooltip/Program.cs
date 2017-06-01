using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Dynamics.Nav.MetaModel;
using Microsoft.Dynamics.Nav.MetaMetaModel;
using Microsoft.Dynamics.Nav.Model.IO.Txt;
using System.Security.Cryptography;

namespace Singhammer.SITE
{
    /// <summary>  
    ///  This class encapsulates a link to an object, the corresponding filename, and the associated tooltips
    /// </summary> 
    class NAVObject
    {
        public int Number { get; set; }
        public ElementType Type { get; set; }
        public string FileName { get; set; }
        public List<Tooltip> Tooltips { get; set; }

        public NAVObject()
        {
            Tooltips = new List<Tooltip>();
        }

        public override int GetHashCode()
        {
            // not perfect, but collisions are ok
            return (int)Type * 1000000 + Number;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is NAVObject))
                return false;
            NAVObject navObject = (NAVObject)obj;
            return (navObject.Number == this.Number && navObject.Type == this.Type);
        }
    }

    /// <summary>  
    ///  This class contains a control's tooltips
    /// </summary> 
    class Tooltip
    {
        public ElementType ElementType { get; set; }
        public int Id { get; set; }
        public IDictionary<string, string> TooltipML { get; set; }

        public Tooltip()
        {
            TooltipML = new Dictionary<string, string>();
        }

        public Tooltip(ElementType ElementType, int Id) : this()
        {
            this.ElementType = ElementType;
            this.Id = Id;
        }

        public Tooltip(ElementType ElementType, int Id, string tooltipML) : this(ElementType, Id)
        {
            LoadProperty(tooltipML);
        }

        public void addText(string language, string text)
        {
            TooltipML.Add(language, text);
        }

        /// <summary>  
        ///  Generates a NAV multilanguage string from the TooltipML property
        /// </summary> 
        /// <param name="includeGenerated">True if generated tooltips (prepended with ###) should be included in the result. False (default) if not</param>
        /// <returns>NAV multilanguage string</returns>
        public string GetTooltipMLProperty(bool includeGenerated = false)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var language in TooltipML)
            {
                if (language.Value.StartsWith("###") && !includeGenerated)
                    continue;

                if (sb.Length > 0)
                    sb.Append(";");
                sb.Append(language.Key);
                sb.Append("=");
                // If a semicolon is present, the textstring must be quoted
                if (language.Value.Contains(';'))
                {
                    sb.Append('"');
                    string quoted = language.Value.Replace("\"", "\"\"");
                    sb.Append(quoted);
                    sb.Append('"');
                }
                else
                    sb.Append(language.Value);
            }
            string value = sb.ToString();
            return value;
        }

        /// <summary>  
        ///  Accepts a NAV multilangue string and loads it into the TooltipML property
        /// </summary> 
        /// <param name="value">The NAV multilanguage string. May contain an arbitrary number of languages</param>
        public void LoadProperty(string value)
        {
            TooltipML = new Dictionary<string, string>();
            var languages = value.Split('\n');
            foreach (var language in languages)
            {
                if (language != "")
                {
                    string[] parts = language.Split(new char[] { '=' }, 2);
                    // Remove separators and double (quadruple) quotes
                    string text = parts[1].Trim().TrimStart('"').TrimEnd(new char[] { ';', '"' });
                    text = text.Replace("\"\"", "\"");
                    text = text.Replace("\"\"", "\"");
                    addText(parts[0], text);
                }
            }
        }
    }

    class Program
    {
        const char delimiter = '\t';

        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine(@"Usage: Tooltip.exe directory\*.txt -export filename.tsv [-generateTooltips]");
                Console.WriteLine(@"Usage: Tooltip.exe directory -import filename.tsv");
                return (-1);
            }

            string sourcePath;
            string sourcePattern;
            string fileName = args[2];
            bool generateTooltips = false;
            switch (args[1])
            {
                case "-export":
                    sourcePath = Path.GetDirectoryName(args[0]);
                    sourcePattern = Path.GetFileName(args[0]);
                    if (args.Length >= 4 && args[3] == "-generateTooltips")
                        generateTooltips = true;
                    ExportTooltips(sourcePath, sourcePattern, fileName, generateTooltips);
                    break;
                case "-import":
                    sourcePath = args[0];
                    ImportTooltips(sourcePath, fileName);
                    break;
            }
            return 0;
        }

        /// <summary>  
        ///  Read all objects in a file
        /// </summary> 
        /// <param name="fileName">The text file. May contain only one or multiple objects</param>
        /// <returns>List of ApplicationObjects</returns>
        public static List<ApplicationObject> ReadNavObjects(string fileName)
        {
            TxtFileModelInfo modelInfo = new TxtFileModelInfo();
            TxtImporter importer = new TxtImporter(modelInfo);
            try
            {
                using (var instream = new FileStream(fileName, FileMode.Open))
                {
                    List<ApplicationObject> objects = importer.ImportFromStream(instream);
                    if (objects != null && objects.Count > 0)
                        return objects;
                    else
                    {
                        Console.WriteLine(@"Object could not be read from file {0}", fileName);
                    }
                }
            }
            catch (Microsoft.Dynamics.Nav.Model.IO.Txt.TxtImportException e)
            {
                Console.WriteLine(@"Exception while reading {0}: {1}", fileName, e.Message);
                Console.WriteLine(@"Source line {0}, col {1}: {2}", e.LineNo, e.LinePos, e.Line);
            }
            catch (System.IO.IOException e)
            {
                Console.WriteLine(@"Exception while reading {0}: {1}", fileName, e.Message);
            }
            return new List<ApplicationObject>();
        }

        /// <summary>  
        ///  Save all objects in the list to a file
        /// </summary> 
        /// <param name="fileName">The text file where objects are written to</param>
        /// <param name="objects">A List of ApplicationObjects</param>
        public static void SaveNavObjects(string fileName, List<ApplicationObject> objects)
        {
            TxtFileModelInfo modelInfo = new TxtFileModelInfo();
            TxtExporter export = new TxtExporter(modelInfo);
            try
            {
                using (var outstream = new FileStream(fileName, FileMode.Create))
                {
                    foreach (var obj in objects)
                    {
                        export.ExportObject(obj, outstream);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(@"Exception while writing {0}: {1}", fileName, e.Message);
            }
        }

        static string GetMD5(string value1, string value2)
        {
            MD5 md5 = MD5.Create();
            StringBuilder sb = new StringBuilder();
            byte[] hash = md5.ComputeHash(Encoding.Unicode.GetBytes(value1 + value2));
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        /// <summary>  
        ///  Loop over several files and write all tooltips to a tab-separated file.
        /// </summary> 
        /// <param name="sourcePath">The directory where the text files are stored</param>
        /// <param name="sourcePattern">Specifies what files to process (e.g. MyObject.txt or *.txt)</param>
        /// <param name="fileName">The output file</param>
        /// <param name="generateTooltips">Generate a tooltip based on CaptionML and SourceExpr properties. </param>
        static void ExportTooltips(string sourcePath, string sourcePattern, string fileName, bool generateTooltips)
        {
            int tooltipCount = 0;
            SortedDictionary<int, NAVObject> objects = new SortedDictionary<int, NAVObject>();
            HashSet<string> duplicates = new HashSet<string>();

            string[] files = Directory.GetFiles(sourcePath, sourcePattern, SearchOption.AllDirectories);
            foreach (string file in files)
            {
                List<ApplicationObject> objectsInFile = ReadNavObjects(file);
                foreach (var objInFile in objectsInFile)
                {
                    var obj = new NAVObject()
                    {
                        Type = objInFile.Id.ObjectType,
                        Number = objInFile.Id.ObjectNumber,
                        FileName = Path.GetFileName(file)
                    };
                    obj.Tooltips = GetTooltips(objInFile, generateTooltips);
                    objects.Add(obj.GetHashCode(), obj);
                }
            }

            StringBuilder sb = new StringBuilder();
            foreach (var obj in objects.Values)
            {
                List<Tooltip> tooltips = obj.Tooltips;
                foreach (Tooltip tooltip in tooltips)
                {
                    sb.Append(obj.FileName);
                    sb.Append(delimiter);
                    sb.Append((int)obj.Type);
                    sb.Append(delimiter);
                    sb.Append((int)obj.Number);
                    sb.Append(delimiter);
                    sb.Append((int)tooltip.ElementType);
                    sb.Append(delimiter);
                    sb.Append(tooltip.ElementType);
                    sb.Append(delimiter);
                    sb.Append(tooltip.Id);
                    foreach (var language in tooltip.TooltipML)
                    {
                        sb.Append(delimiter);
                        sb.Append(language.Key);
                        sb.Append(delimiter);
                        sb.Append(language.Value);
                        sb.Append(delimiter);
                        //duplicate?
                        string hash = GetMD5(language.Key, language.Value);
                        if (duplicates.Contains(hash))
                        {
                            sb.Append("1");
                        }
                        else
                        {
                            sb.Append("0");
                            duplicates.Add(hash);
                        }
                    }
                    sb.Append(System.Environment.NewLine);
                    tooltipCount++;
                }
            }
            try
            {
                File.WriteAllText(fileName, sb.ToString(), Encoding.GetEncoding("utf-8"));
                Console.WriteLine("{0} object(s) with {1} tooltip(s) processed.", objects.Count(), tooltipCount);
            }
            catch (System.IO.IOException e)
            {
                Console.WriteLine(@"Error while writing {0}: {1}", fileName, e.Message);
            }
        }

        /// <summary>  
        ///  Loop over several files and write all tooltips to a tab-separated file.
        /// </summary> 
        /// <param name="sourcePath">The directory where the text files are stored</param>
        /// <param name="sourcePattern">Specifies what files to process (e.g. MyObject.txt or *.txt)</param>
        /// <param name="fileName">The output file</param>
        /// <param name="generateTooltips">Generate a tooltip based on CaptionML and SourceExpr properties. </param>
        public static List<Tooltip> GetTooltips(ApplicationObject obj, bool generateTooltips)
        {
            List<Tooltip> tooltips = new List<Tooltip>();
            foreach (IElement element in obj.GetElements())
            {
                string value = null;
                try
                {
                    value = element.GetStringProperty(PropertyType.ToolTipML);
                    if (value != null)
                    {
                        var tooltip = new Tooltip(element.ElementTypeInfo.ElementType, element.Id, value);
                        if (tooltip.GetTooltipMLProperty(false) != "")
                            tooltips.Add(tooltip);
                    }
                    else if (generateTooltips)
                    {
                        var generatedTooltip = GenerateTooltip(element);
                        if (generatedTooltip.GetTooltipMLProperty(true) != "")
                            tooltips.Add(generatedTooltip);
                    }
                }
                catch (Exception) { }
            }
            return tooltips;
        }

        /// <summary>  
        ///  Tries to generate a tooltip based on CaptionML and SourceExpr properties
        ///  Tooltips generated out of captions will be marked with ###
        ///  Tooltips generated out of SourceExpr captions will be marked with language code @@@
        /// </summary> 
        /// <param name="element">The element to generate a tooltip for</param>
        public static Tooltip GenerateTooltip(IElement element)
        {
            Tooltip tooltip = null;
            string value;

            if (element.TryGetStringProperty(PropertyType.CaptionML, out value) && value != null)
            {
                tooltip = new Tooltip(element.ElementTypeInfo.ElementType, element.Id, value);
            }
            else if (element.TryGetStringProperty(PropertyType.SourceExpression, out value) && value != null)
            {
                tooltip = new Tooltip(element.ElementTypeInfo.ElementType, element.Id);
                tooltip.addText("@@@", value);
            }

            if (tooltip != null && tooltip.GetTooltipMLProperty(true) != "")
            {
                Tooltip generatedTooltip = new Tooltip(tooltip.ElementType, tooltip.Id);
                foreach (var item in tooltip.TooltipML)
                {
                    if (item.Value != "")
                        generatedTooltip.addText(item.Key, "### " + item.Value);
                }
                return generatedTooltip;
            }
            return null;
        }

        /// <summary>  
        ///  Loop over several files and write all tooltips to a tab-separated file.
        /// </summary> 
        /// <param name="sourcePath">The directory where the text files are stored</param>
        /// <param name="sourcePattern">Specifies what files to process (e.g. MyObject.txt or *.txt)</param>
        /// <param name="fileName">The output file</param>
        static void ImportTooltips(string sourcePath, string fileName)
        {
            int tooltipCount = 0;
            SortedDictionary<int, NAVObject> objects = new SortedDictionary<int, NAVObject>();
            try
            {
                string[] lines = File.ReadAllLines(fileName, Encoding.GetEncoding("utf-8"));
                foreach (var line in lines)
                {
                    string[] col = line.Split(delimiter);

                    try
                    {
                        var obj = new NAVObject()
                        {
                            Type = (ElementType)int.Parse(col[1]),
                            Number = int.Parse(col[2]),
                            FileName = col[0],
                        };

                        var tooltip = new Tooltip()
                        {
                            ElementType = (ElementType)int.Parse(col[3]),
                            Id = int.Parse(col[5])
                        };

                        for (int i = 6; i < col.Length; i += 3)
                        {
                            if (col[i] != "")
                                tooltip.addText(col[i], col[i + 1]);
                        }

                        if (!objects.ContainsKey(obj.GetHashCode()))
                        {
                            objects.Add(obj.GetHashCode(), obj);
                        }
                        else
                        {
                            obj = objects[obj.GetHashCode()];
                        }
                        obj.Tooltips.Add(tooltip);
                    }
                    catch (FormatException) { }
                }
            }
            catch (System.IO.IOException e)
            {
                Console.WriteLine(@"Exception while reading {0}: {1}", fileName, e.Message);
            }

            string lastFileName = "";
            List<ApplicationObject> objectsInFile = new List<ApplicationObject>();
            foreach (NAVObject obj in objects.Values)
            {
                string objectFilename = sourcePath + Path.DirectorySeparatorChar + obj.FileName;
                if (objectFilename != lastFileName)
                {
                    if (lastFileName != "")
                        SaveNavObjects(lastFileName, objectsInFile);
                    objectsInFile = ReadNavObjects(objectFilename);
                    lastFileName = objectFilename;
                    Console.WriteLine(@"Processing {0}", objectFilename);
                }
                var objInFile = objectsInFile.Find(x => x.Id.ObjectType == obj.Type && x.Id.ObjectNumber == obj.Number);
                if (objInFile != null)
                {
                    foreach (Tooltip tooltip in obj.Tooltips)
                    {
                        IEnumerable<IElement> elements =
                            from element in objInFile.GetElements()
                            where element.Id == tooltip.Id && element.ElementTypeInfo.ElementType == tooltip.ElementType
                            select element;
                        try
                        {
                            if (elements.Count() != 0)
                            {
                                string value = tooltip.GetTooltipMLProperty();
                                if (value != "")
                                    elements.First().SetStringProperty(PropertyType.ToolTipML, tooltip.GetTooltipMLProperty());
                            }
                        }
                        catch (ArgumentException e)
                        {
                            Console.WriteLine(@"Exception while setting property in {0} control id {1}", objectFilename, elements.First().Id);
                            Console.WriteLine(e.Message);
                        }
                        tooltipCount++;
                    }
                }
            }
            if (lastFileName != "")
                SaveNavObjects(lastFileName, objectsInFile);

            Console.WriteLine("{0} object(s) with {1} tooltip(s) processed.", objects.Count(), tooltipCount);
        }
    }
}

