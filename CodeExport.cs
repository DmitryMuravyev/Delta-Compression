/*

MIT License

Copyright (c) 2024 Dmitry Muravyev (youtube.com/@DmitryMuravyev)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.Linq;
using System.Xml.XPath;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Configuration;


 
namespace DeltaComp
{
    // The class is dedicated to save compressed data to a header file for a specified platform.
    public class CodeExport
    {

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private constants/attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        
        // Some static constants
        private static class Constants
        {
            // The maximum number of elements in a string used to write an array of compressed data
            // in the form of a hexadecimal byte values list.
            public const int ElementsPerRow = 100;
            // Write lock request timeout in case we need to write an array by several threads.
            public const int WriterLockTimeout = 1000; // You might wanna change timeout value
            // The limit for issuing an array size warning.
            public const string DataNodeXPath = "//data[@platform='{0}']";
            public const string AllPlatformsIdentifier = "all";
            public const string ChunkSizeAttribute = "chunkSize";
            public const string LimitStringFormat = "// Limit of {0} bytes reached.";

            // Error messages
            public const string ExportAlreadyRunning = "The export already started. To start a new one, you need to finish the current one.";
            public const string SourceFilesNotFoundError = "The source XML and XSLT files were not found.";
            public const string SourceFilesNotSpecified = "The names of the source XML and XSLT files are not specified.";
            public const string SourceXmlWrongFormat = "Wrong source XML format.";
            public const string ExportNotStarted = "Export not started.";
            public const string ENSOrInitializedWOArray = "The export was not started or was initialized without exporting the array.";
        }

        // Export environment
        private StreamWriter? _outputStreamWriter = null;
        private bool _exportArrayNeeded = false;
        private bool _isFirstElementWritten = false;
        private int _elementColumn = 0;
        private int _elementIndex = 0;

        // We will write an array not from the main thread,
        // but there will only be one writing thread, so we do not need a lock.
        //private ReaderWriterLock locker = new ReaderWriterLock();


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Names or full paths to the XML/XSLT files used to generate .h
        public string? XmlFile = null;
        public string? XsltFile = null;
        public int ChunkSizeWarning = -1;
        // We will not return an error message while writing array elements,
        // so we will save it in a separate variable to read it later.
        public string? LastErrorMessage = null;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Constructor
        public CodeExport(string xmlFile, string xsltFile)
        {
            // Init base variables
            XmlFile = xmlFile;
            XsltFile = xsltFile;
        }


        // Destructor
        ~CodeExport()
        {
            _outputStreamWriter?.Dispose();
            _outputStreamWriter = null;
        }


        // Export preparation: basic checks and writing the top block of the header file.
        public bool PrepareExport(bool withArray, Dictionary<string, string> arguments, string platform,
            string sourceDataName, string baseName, string outputFile, out string errorMessage)
        {
            // Check if the export has already been started, but not completed.
            if (_outputStreamWriter != null)
            {
                LastErrorMessage = Constants.ExportAlreadyRunning;
                errorMessage = LastErrorMessage;
                return false;
            }

            _exportArrayNeeded = withArray;
            bool platformDefined = (platform.Length > 0);

            // Check if the platform is specified.
            if (platformDefined) {

                // Check XML/XSLT file names
                if ((XmlFile == null) || (XsltFile == null))
                {
                    LastErrorMessage = Constants.SourceFilesNotSpecified;
                    errorMessage = LastErrorMessage;
                    return false;
                }

                // Try to find the XML/XSLT file paths
                if (!CheckSourceFilesExist())
                {
                    if (!TrySourceFilesPath(Environment.CurrentDirectory))
                    {
                        string? applicationPath = Path.GetDirectoryName(Environment.ProcessPath);
                        if (applicationPath == null) applicationPath = AppContext.BaseDirectory;
                        //Debug.WriteLine($"Physical location {AppDomain.CurrentDomain.BaseDirectory}");
                        if (!TrySourceFilesPath(applicationPath))
                        {
                            LastErrorMessage = Constants.SourceFilesNotFoundError;
                            errorMessage = LastErrorMessage;
                            return false;
                        }
                    }
                }

                // Fill basic XSLT arguments
                XsltArgumentList xsltArgumentList = new XsltArgumentList();
                xsltArgumentList.AddParam("FullName", "", sourceDataName);
                xsltArgumentList.AddParam("BaseName", "", baseName);
                xsltArgumentList.AddParam("Platform", "", platform);
                //if (withArray) xsltArgumentList.AddParam("DataSize", "", arraySize.ToString());

                // And add an argument with a key/value list to pass the values of the main parameters.
                XElement el = new XElement("root", arguments.Select(kv => new XElement("element",
                                new XAttribute("key", kv.Key), new XAttribute("value", kv.Value))));
                xsltArgumentList.AddParam("Defines", "", el.CreateNavigator());

                // Load source XML
                XmlDocument sourceXml = new XmlDocument();
                try
                {
                    sourceXml.Load(XmlFile);
                }
                catch (Exception ex)
                {
                    LastErrorMessage = ex.Message;
                    errorMessage = LastErrorMessage;
                    return false;
                }

                // Try to get chunk size warning parameter
                XmlElement? dataElement = 
                    sourceXml.SelectSingleNode(String.Format(Constants.DataNodeXPath, platform)) as XmlElement;
                if (dataElement == null) dataElement = 
                        sourceXml.SelectSingleNode(String.Format(Constants.DataNodeXPath, Constants.AllPlatformsIdentifier)) as XmlElement;
                if (dataElement != null)
                {
                    int.TryParse(dataElement.GetAttribute(Constants.ChunkSizeAttribute), out ChunkSizeWarning);
                }

                XPathNavigator? sourceXmlNavigator = sourceXml.CreateNavigator();
                if (sourceXmlNavigator == null)
                {
                    LastErrorMessage = Constants.SourceXmlWrongFormat;
                    errorMessage = LastErrorMessage;
                    return false;
                }

                // Perform transformation
                using (XmlTextWriter headerWriter = new XmlTextWriter(outputFile, null))
                {
                    XslCompiledTransform xslt = new XslCompiledTransform();
                    try
                    {
                        xslt.Load(XsltFile);
                        xslt.Transform(sourceXmlNavigator, xsltArgumentList, headerWriter);
                    }
                    catch (Exception ex)
                    {
                        LastErrorMessage = ex.Message;
                        errorMessage = LastErrorMessage;
                        // All the "finally" blocks (which is below and which is implemented by the "using" block)
                        // will be executed despite the "return" statement.
                        return false;
                    }
                    finally
                    {
                        if ((headerWriter != null) && (headerWriter.BaseStream != null))
                        {
                            headerWriter.Flush();
                            headerWriter.Close();
                        }
                    }
                }

            }

            // Preparing to export an array of elements (if we need it).
            if (_exportArrayNeeded) {
                _isFirstElementWritten = false;
                _elementColumn = 0;

                try
                {
                    // Open file for appending
                    _outputStreamWriter = File.AppendText(outputFile);
                    if (platformDefined)
                    {
                        _outputStreamWriter.WriteLine("{");
                        _outputStreamWriter.Write("\t");
                    }
                    else
                    {
                        _outputStreamWriter.Write("{");
                    }
                }
                catch (Exception ex)
                {
                    LastErrorMessage = ex.Message;
                    errorMessage = LastErrorMessage;

                    // Cleenup if something went wrong
                    if (_outputStreamWriter != null)
                    {
                        if (_outputStreamWriter.BaseStream != null) _outputStreamWriter.Close();
                        _outputStreamWriter.Dispose();
                        _outputStreamWriter = null;
                    }

                    return false;
                }
            }

            LastErrorMessage = null;
            errorMessage = "";
            return true;
        }


        // Write next array element
        public bool AppendArrayElement(byte element)
        {
            if ((_outputStreamWriter != null) && _exportArrayNeeded)
            {
                try
                {
                    //We don't need no locker, let the motherfucker crash...
                    //locker.AcquireWriterLock(Constants.WriterLockTimeout);

                    if (_isFirstElementWritten)
                    {
                        _outputStreamWriter.Write(", ");
                    }
                    else
                    {
                        _isFirstElementWritten = true;
                    }

                    // For some platforms (e.g. AVR), we have to split array into several parts < 32K
                    // (later we will do it automatically, but for now we just place a warning).
                    if ((ChunkSizeWarning > 0) && (_elementIndex >= ChunkSizeWarning))
                    {
                        _outputStreamWriter.WriteLine("");
                        _outputStreamWriter.WriteLine(String.Format(Constants.LimitStringFormat, ChunkSizeWarning));
                        _outputStreamWriter.Write("\t");
                       _elementIndex = 0;
                        _elementColumn = 0;
                    }

                    if (_elementColumn >= Constants.ElementsPerRow)
                    {
                        _outputStreamWriter.WriteLine("");
                        _outputStreamWriter.Write("\t");
                        _elementColumn = 0;
                    }

                    _outputStreamWriter.Write("0x{0:X2}", element);
                    _elementColumn++;
                    _elementIndex++;

                    return true;
                }
                catch (Exception ex)
                {
                    LastErrorMessage = ex.Message;
                    return false;
                }

                // Crash motherfucker, crash...
                /*
                finally
                {
                    locker.ReleaseWriterLock();
                }
                */
            }

            LastErrorMessage = Constants.ENSOrInitializedWOArray;
            return false;
        }


        // Write the footer to the output file and close it.
        public bool FinalizeExport(out string errorMessage)
        {
            if (_outputStreamWriter != null)
            {
                try
                {
                    if (_exportArrayNeeded) _outputStreamWriter.WriteLine("};");
                    _outputStreamWriter.WriteLine("");
                    _outputStreamWriter.WriteLine("// END-OF-FILE");
                    _outputStreamWriter.Flush();
                    _outputStreamWriter.Close();
                }
                catch (Exception ex)
                {
                    LastErrorMessage = ex.Message;
                    errorMessage = LastErrorMessage;
                    return false;
                }
                finally
                {
                    _outputStreamWriter.Dispose();
                    _outputStreamWriter = null;
                }

                LastErrorMessage = null;
                errorMessage = "";
                return true;
            }

            LastErrorMessage = Constants.ExportNotStarted;
            errorMessage = LastErrorMessage;
            return false;
        }


        // Check if XML and XSLT files exist
        private bool CheckSourceFilesExist()
        {
            if (File.Exists(XmlFile) && File.Exists(XsltFile)) return true;
            return false;
        }


        // Check if XML and XSLT files exist in the specified path
        private bool TrySourceFilesPath(string path)
        {
            if ((XmlFile == null) || (XsltFile == null)) return false;
            string xmlFullPath = Path.Combine(path, Path.GetFileName(XmlFile).ToString());
            string xsltFullPath = Path.Combine(path, Path.GetFileName(XsltFile));
            if (File.Exists(xmlFullPath) && File.Exists(xsltFullPath))
            {
                XmlFile = xmlFullPath;
                XsltFile = xsltFullPath;
                return true;
            }
            return false;
        }

    }
}

// END-OF-FILE
