using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;

namespace XML2Classes
{
    public partial class XML2Classes : Form
    {
        private FileStream outFileStream;
        private List<XMLClass> lstClassList;
        public XML2Classes()
        {
            InitializeComponent();
        }

        private void btnOpenFile_Click(object sender, EventArgs e)
        {
            openXMLFileDialog.Filter = "XML Files (.xml)|*.xml";
            openXMLFileDialog.Multiselect = false;
            openXMLFileDialog.ShowDialog();
            string XMLFileName = openXMLFileDialog.FileName;
            textXMLFilePath.Text = XMLFileName;
        }

        private void btnConvert_Click(object sender, EventArgs e)
        {
            lstClassList = new List<XMLClass>();
            if (0 == textXMLFilePath.Text.Trim().Length)
            {
                MessageBox.Show("Please enter a file name in the textbox", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textXMLFilePath.Focus();
                return;
            }
            String inputXMLFileName = textXMLFilePath.Text;
            String outputClassFileName = inputXMLFileName.Substring(0, inputXMLFileName.LastIndexOf(@".")) + ".h";
            //MessageBox.Show(outputClassFileName);
            outFileStream = new FileStream(outputClassFileName, FileMode.Create, FileAccess.Write);

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            settings.ProhibitDtd = false;

            XmlReader reader = XmlReader.Create(inputXMLFileName, settings);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(reader);

            XmlNode rootNode = xmlDoc.SelectSingleNode("/*");
            try
            {
                progressBarConversion.Style = ProgressBarStyle.Continuous;
                progressBarConversion.Minimum = 0;                
                progressBarConversion.Show();
                parseXMLToClasses(rootNode);
                //lstClassList is updated; write to file
                progressBarConversion.Value = 0;
                progressBarConversion.Maximum = lstClassList.Count;
                WriteToFile(lstClassList);
                MessageBox.Show("C++ code generated and saved as : " + outputClassFileName, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                MessageBox.Show("Error occured while parsing the XML: " + exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //if (System.Windows.Forms.Application.MessageLoop)
                //{
                //    // Use this since we are a WinForms app
                //    System.Windows.Forms.Application.Exit();
                //}
            }
            finally
            {
                outFileStream.Close();                
            }
        }

        private void WriteToFile(List<XMLClass> lstClass)
        {
            //add headers;
            string strHeaders = "#pragma once\n";
            strHeaders += "#include <iostream>\n";
            strHeaders += "#include <vector>\n";
            strHeaders += "using namespace std;\n";
            ASCIIEncoding asciiEncoding = new ASCIIEncoding();
            outFileStream.Write(asciiEncoding.GetBytes(strHeaders), 0, asciiEncoding.GetByteCount(strHeaders));

            //flush definitions
            lstClass.Reverse();
            foreach (XMLClass xmlclass in lstClass)
            {
                progressBarConversion.Value++;
                string classText = "class ";
                classText += xmlclass.strClassName + "\n{\n";
                foreach (KeyValuePair<ClassMember, int> kvp in xmlclass.dictMemberCount)
                {
                    if (kvp.Value <= 1)
                    {
                        classText += "\tprivate: " + kvp.Key.Type + " " + kvp.Key.Name + ";\n";          //declare field
                        classText += "\tpublic: void set" + kvp.Key.Name + "(const " + kvp.Key.Type + " &IN" + kvp.Key.Name + "){ " + kvp.Key.Name + " = IN" + kvp.Key.Name + "; }\n";      //setter
                        classText += "\tpublic: " + kvp.Key.Type + " get" + kvp.Key.Name + "(){ return " + kvp.Key.Name + "; }\n";      //getter 
                    }
                    else
                    {
                        classText += "\tprivate: vector<" + kvp.Key.Type + "> " + kvp.Key.Name + ";\n";          //declare field
                        classText += "\tpublic: void set" + kvp.Key.Name + "(const vector<" + kvp.Key.Type + "> &IN" + kvp.Key.Name + "){ " + kvp.Key.Name + " = IN" + kvp.Key.Name + "; }\n";      //setter
                        classText += "\tpublic: vector<" + kvp.Key.Type + "> get" + kvp.Key.Name + "(){ return " + kvp.Key.Name + "; }\n";      //getter
                    }
                }
                classText += "};\n";

                outFileStream.Write(asciiEncoding.GetBytes(classText), 0, asciiEncoding.GetByteCount(classText));
            }
        }

        private bool isValidClassCandidate(XmlNode node)
        {
            //bool bValidNode = true;
            if (node.NodeType == XmlNodeType.EndElement)
            {
                return false;
            }
            if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA)
            {
                return false;
            }
            if (node.FirstChild != null && (node.FirstChild.NodeType == XmlNodeType.Text || node.FirstChild.NodeType == XmlNodeType.CDATA))
            {
                return false;
            }
            return true;
        }

        private bool isValidMemberCandidate(XmlNode node)
        {
            if (node.NodeType == XmlNodeType.EndElement)
            {
                return false;
            }
            if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA)
            {
                return false;
            }
            return true;
        }

        private void parseXMLToClasses(XmlNode rootNode)
        {

            if (!isValidClassCandidate(rootNode))
            {
                return;
            }
            string strNodeName = rootNode.Name;
            XmlAttributeCollection attributes = rootNode.Attributes;
            XmlNodeList children = rootNode.ChildNodes;
            XMLClass xmlClass = new XMLClass();
            xmlClass.strClassName = strNodeName;
            foreach (XmlAttribute attr in attributes)
            {
                if (attr.Name.Contains("xmlns"))
                {
                    continue;
                }
                ClassMember member = new ClassMember();
                member.Type = "string";
                member.Name = attr.Name;
                if (!xmlClass.dictMemberCount.ContainsKey(member))
                {
                    xmlClass.dictMemberCount.Add(member, 1);
                }
                else
                {
                    xmlClass.dictMemberCount[member]++;
                }
            }
            foreach (XmlNode child in children)
            {
                if (!isValidMemberCandidate(child))
                {
                    continue;
                }
                else if (child.FirstChild != null && (child.FirstChild.NodeType == XmlNodeType.Text || child.FirstChild.NodeType == XmlNodeType.CDATA))
                {
                    ClassMember member = new ClassMember();
                    member.Type = "string";
                    member.Name = child.Name;
                    if (!xmlClass.dictMemberCount.ContainsKey(member))
                    {
                        xmlClass.dictMemberCount.Add(member, 1);
                    }
                    else
                    {
                        xmlClass.dictMemberCount[member]++;
                    }
                }
                else
                {
                    ClassMember member = new ClassMember();
                    member.Type = child.Name;
                    member.Name = child.Name + "Field";
                    if (!xmlClass.dictMemberCount.ContainsKey(member))
                    {
                        xmlClass.dictMemberCount.Add(member, 1);
                    }
                    else
                    {
                        xmlClass.dictMemberCount[member]++;
                    }
                }
            }
            if (!lstClassList.Contains(xmlClass))
            {
                lstClassList.Add(xmlClass);
            }

            //call recursively            
            foreach (XmlNode child in children)
            {
                parseXMLToClasses(child);
            }

        }
    }
    public class XMLClass
    {
        public string strClassName;
        public Dictionary<ClassMember, int> dictMemberCount;
        public XMLClass()
        {
            dictMemberCount = new Dictionary<ClassMember, int>(new ClassMemberComparer());
        }

        public override bool Equals(object obj)
        {
            XMLClass y = obj as XMLClass;
            return string.Equals(this.strClassName, y.strClassName);
        }
    }
    public class ClassMember
    {
        public string Type;
        public string Name;
    }

    public sealed class ClassMemberComparer : IEqualityComparer<ClassMember>
    {
        public bool Equals(ClassMember x, ClassMember y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return string.Equals(x.Type, y.Type) && string.Equals(x.Name, y.Name);
        }

        public int GetHashCode(ClassMember obj)
        {
            unchecked
            {
                return ((obj.Type != null ? obj.Type.GetHashCode() : 0) * 397) ^ ((obj.Name != null ? obj.Name.GetHashCode() : 0) * 37);
            }
        }
    }

    public sealed class xmlClassComparer : IEqualityComparer<XMLClass>
    {
        public bool Equals(XMLClass x, XMLClass y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return string.Equals(x.strClassName, y.strClassName);
        }

        public int GetHashCode(XMLClass obj)
        {
            unchecked
            {
                return (obj.strClassName != null ? obj.strClassName.GetHashCode() : 0);
            }
        }
    }
}
