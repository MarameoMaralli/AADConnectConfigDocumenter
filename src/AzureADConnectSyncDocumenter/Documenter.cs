﻿//------------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="Documenter.cs" company="Microsoft">
//      Copyright (c) Microsoft. All Rights Reserved.
//      Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
// </copyright>
// <summary>
// Base class for Azure AD Connect Sync Configuration Documenter
// </summary>
//------------------------------------------------------------------------------------------------------------------------------------------

namespace AzureADConnectConfigDocumenter
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Data;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Web.UI;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.XPath;

    /// <summary>
    /// The abstract base class for the documenters of all other types of AAD Connect sync configuration items.
    /// </summary>
    public abstract class Documenter
    {
        /// <summary>
        /// The name of the column which stores the row state of the row in a diffgram.
        /// </summary>
        public const string RowStateColumn = "ROW-STATE";

        /// <summary>
        /// The prefix used for the name of the columns of old data row in a diffgram 
        /// </summary>
        public const string OldColumnPrefix = "OLD-";

        /// <summary>
        /// The lower case letters
        /// </summary>
        public const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// The upper case letters
        /// </summary>
        public const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        /// The row error text added to identify a row that was to an empty table so that it prints better
        /// Such row is not printed if it appears as a deleted row in the diffgram
        /// </summary>
        public const string VanityRow = "~~VANITY_ROW~~";

        /// <summary>
        /// The namespace manager
        /// </summary>
        private static XmlNamespaceManager namespaceManager = new XmlNamespaceManager(new NameTable());

        /// <summary>
        /// Initializes static members of the <see cref="Documenter"/> class.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Reviewed.")]
        static Documenter()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                Documenter.namespaceManager.AddNamespace("dsml", Documenter.DsmlNamespace.NamespaceName);
                Documenter.namespaceManager.AddNamespace("ms-dsml", Documenter.MmsDsmlNamespace.NamespaceName);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Documenter"/> class.
        /// </summary>
        protected Documenter()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                // Set Logger call context items
                Logger.FlushContextItems();
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Documenter"/> class.
        /// </summary>
        ~Documenter()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                if (File.Exists(this.ReportFileName))
                {
                    File.Delete(this.ReportFileName);
                }

                if (File.Exists(this.ReportToCFileName))
                {
                    File.Delete(this.ReportToCFileName);
                }
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Gets the report folder.
        /// </summary>
        /// <value>
        /// The report folder.
        /// </value>
        public static string ReportFolder
        {
            get
            {
                var rootDirectory = Directory.GetCurrentDirectory().TrimEnd('\\');
                var reportFolder = rootDirectory + @"\Report";
                if (!Directory.Exists(reportFolder))
                {
                    Directory.CreateDirectory(reportFolder);
                }

                return reportFolder;
            }
        }

        /// <summary>
        /// Gets the namespace manager.
        /// </summary>
        /// <value>
        /// The namespace manager.
        /// </value>
        protected static XmlNamespaceManager NamespaceManager
        {
            get { return Documenter.namespaceManager; }
        }

        /// <summary>
        /// Gets the DSML namespace.
        /// </summary>
        /// <value>
        /// The DSML namespace.
        /// </value>
        protected static XNamespace DsmlNamespace
        {
            get { return "http://www.dsml.org/DSML"; }
        }

        /// <summary>
        /// Gets the MMS DSML namespace.
        /// </summary>
        /// <value>
        /// The MMS DSML namespace.
        /// </value>
        protected static XNamespace MmsDsmlNamespace
        {
            get { return "http://www.microsoft.com/MMS/DSML"; }
        }

        /// <summary>
        /// Gets or sets the consolidated pilot XML.
        /// </summary>
        /// <value>
        /// The pilot XML.
        /// </value>
        protected XElement PilotXml { get; set; }

        /// <summary>
        /// Gets or sets the consolidated production XML.
        /// </summary>
        /// <value>
        /// The production XML.
        /// </value>
        protected XElement ProductionXml { get; set; }

        /// <summary>
        /// Gets or sets the pilot data set.
        /// </summary>
        /// <value>
        /// The pilot data set.
        /// </value>
        protected DataSet PilotDataSet { get; set; }

        /// <summary>
        /// Gets or sets the production data set.
        /// </summary>
        /// <value>
        /// The production data set.
        /// </value>
        protected DataSet ProductionDataSet { get; set; }

        /// <summary>
        /// Gets or sets the diffgram data set.
        /// </summary>
        /// <value>
        /// The diffgram data set.
        /// </value>
        protected DataSet DiffgramDataSet { get; set; }

        /// <summary>
        /// Gets or sets the main report writer.
        /// </summary>
        /// <value>
        /// The main report writer.
        /// </value>
        protected XhtmlTextWriter ReportWriter { get; set; }

        /// <summary>
        /// Gets or sets the report Table of Content writer.
        /// </summary>
        /// <value>
        /// The report Table of Content writer.
        /// </value>
        protected XhtmlTextWriter ReportToCWriter { get; set; }

        /// <summary>
        /// Gets or sets the name of the report file.
        /// </summary>
        /// <value>
        /// The name of the report file.
        /// </value>
        protected string ReportFileName { get; set; }

        /// <summary>
        /// Gets or sets the name of the report to c file.
        /// </summary>
        /// <value>
        /// The name of the report to c file.
        /// </value>
        protected string ReportToCFileName { get; set; }

        /// <summary>
        /// Gets the temporary file path.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns>
        /// The temporary file path.
        /// </returns>
        public static string GetTempFilePath(string fileName)
        {
            fileName = Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c, '-'));

            return Path.GetTempPath() + @"\" + fileName;
        }

        /// <summary>
        /// Gets the type of the attribute from it's DSML syntax.
        /// </summary>
        /// <param name="syntax">The DSML syntax of the attribute.</param>
        /// <param name="indexable">If <c>"true"</c>, the attribute is indexable.</param>
        /// <returns>
        /// The type of the attribute.
        /// </returns>
        public static string GetAttributeType(string syntax, string indexable)
        {
            Logger.Instance.WriteMethodEntry("Syntax: '{0}'. Indexable: '{1}'.", syntax, indexable);

            var attributeType = syntax;

            try
            {
                var attributeSuffix = (indexable == "true") ? " (indexable)" : " (non-indexable)";

                attributeType = Documenter.GetAttributeType(syntax) + attributeSuffix;

                return attributeType;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Syntax: '{0}'. Indexable: '{1}'. Attribute Type: '{2}'.", syntax, indexable, attributeType);
            }
        }

        /// <summary>
        /// Gets the type of the attribute from it's DSML syntax.
        /// </summary>
        /// <param name="syntax">The DSML syntax of the attribute.</param>
        /// <returns>
        /// Returns the type of the attribute.
        /// </returns>
        public static string GetAttributeType(string syntax)
        {
            Logger.Instance.WriteMethodEntry("Syntax: '{0}'.", syntax);

            var attributeType = syntax;

            try
            {
                switch (syntax)
                {
                    case "1.3.6.1.4.1.1466.115.121.1.27":
                    case "1.2.840.113556.1.4.906":
                        attributeType = "Number";
                        break;
                    case "1.3.6.1.4.1.1466.115.121.1.7":
                        attributeType = "Boolean";
                        break;
                    case "1.3.6.1.4.1.1466.115.121.1.5":
                    case "1.3.6.1.4.1.1466.115.121.1.40":
                        attributeType = "Binary";
                        break;
                    case "1.3.6.1.4.1.1466.115.121.1.15":
                    case "1.2.840.113556.1.4.1221":
                    case "1.2.840.113556.1.4.905":
                        attributeType = "String";
                        break;
                    case "1.3.6.1.4.1.1466.115.121.1.12":
                        attributeType = "Reference (DN)";
                        break;
                    case "1.3.6.1.4.1.1466.115.121.1.24":
                        attributeType = "DateTime";
                        break;
                }

                return attributeType;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Syntax: '{0}'. Attribute Type: '{1}'.", syntax, attributeType);
            }
        }

        /// <summary>
        /// Gets the metaverse configuration report.
        /// </summary>
        /// <returns>The Tuple of configuration report and associated TOC</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "The method performs a time-consuming operation.")]
        public abstract Tuple<string, string> GetReport();

        /// <summary>
        /// Gets the Bookmark code for the specified bookmark text.
        /// </summary>
        /// <param name="text">The Bookmark text.</param>
        /// <param name="sectionGuid">The section unique identifier.</param>
        /// <returns>
        /// The Bookmark code for the given bookmark text.
        /// </returns>
        protected static string GetBookmarkCode(string text, string sectionGuid)
        {
            Logger.Instance.WriteMethodEntry("Bookmark Text: '{0}'. Section Guid: '{1}'.", text, sectionGuid);

            var bookmarkCode = string.Empty;

            try
            {
                // MS Word does not like "-" in the bookmarks
                bookmarkCode = (sectionGuid + text).ToUpperInvariant().GetHashCode().ToString(CultureInfo.InvariantCulture).Replace("-", "_");

                return bookmarkCode;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Bookmark Text: '{0}'. Section Guid: '{1}'. Bookmark Code: '{2}'.", text, sectionGuid, bookmarkCode);
            }
        }

        /// <summary>
        /// Gets the diffgram.
        /// </summary>
        /// <param name="pilotDataSet">The pilot data set.</param>
        /// <param name="productionDataSet">The production data set.</param>
        /// <returns>
        /// An <see cref="DataSet"/> object representing the diffgram of the two data sets.
        /// </returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "No good reason to call Dispose() on DataSet.")]
        protected static DataSet GetDiffgram(DataSet pilotDataSet, DataSet productionDataSet)
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                if (pilotDataSet == null)
                {
                    throw new ArgumentNullException("pilotDataSet");
                }

                if (productionDataSet == null)
                {
                    throw new ArgumentNullException("productionDataSet");
                }

                var diffGramDataSet = new DataSet(pilotDataSet.DataSetName) { Locale = CultureInfo.InvariantCulture };

                var printTable = pilotDataSet.Tables["PrintSettings"];

                for (var i = 0; i < pilotDataSet.Tables.Count; ++i)
                {
                    if (pilotDataSet.Tables[i].TableName == "PrintSettings")
                    {
                        continue;
                    }

                    var sortOrder = printTable.Select("SortOrder <> -1 AND TableIndex = " + i, "SortOrder").Select(row => (int)row["ColumnIndex"]).ToArray();
                    var columnsIgnored = printTable.Select("ChangeIgnored = true AND TableIndex = " + i).Select(row => (int)row["ColumnIndex"]).ToArray();

                    var diffGramTable = Documenter.GetDiffgram(pilotDataSet.Tables[i], productionDataSet.Tables[i], columnsIgnored);
                    diffGramTable = Documenter.SortTable(diffGramTable, sortOrder);

                    diffGramDataSet.Tables.Add(diffGramTable);
                }

                diffGramDataSet.Tables.Add(printTable.Copy());

                return diffGramDataSet;
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Gets the diffgram.
        /// </summary>
        /// <param name="pilotTable">The pilot table.</param>
        /// <param name="productionTable">The production table.</param>
        /// <param name="columnsIgnored">The columns ignored when calculating diffgram.</param>
        /// <returns>
        /// An <see cref="DataTable" /> object representing the diffgram of the two tables.
        /// </returns>
        protected static DataTable GetDiffgram(DataTable pilotTable, DataTable productionTable, int[] columnsIgnored)
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                if (columnsIgnored == null)
                {
                    columnsIgnored = new int[] { -1 };
                }

                // Unchanged rows in pilotTable: pilotRow PrimaryKey matches productionRow PrimaryKey AND pilotRow also matches productionRow:
                var unchangedPilotRows = from pilotRow in pilotTable.AsEnumerable()
                                         from productionRow in productionTable.AsEnumerable()
                                         where pilotTable.PrimaryKey.Aggregate(true, (match, keyColumn) => match && pilotRow[keyColumn].Equals(productionRow[keyColumn.Ordinal]))
                                               && pilotRow.ItemArray.Where((item, index) => !columnsIgnored.Contains(index)).SequenceEqual(productionRow.ItemArray.Where((item, index) => !columnsIgnored.Contains(index)))
                                         select pilotRow;

                // Unchanged rows in productionTable: pilotRow PrimaryKey matches productionRow PrimaryKey AND pilotRow also matches productionRow:
                var unchangedProductionRows = from pilotRow in pilotTable.AsEnumerable()
                                              from productionRow in productionTable.AsEnumerable()
                                              where pilotTable.PrimaryKey.Aggregate(true, (match, keyColumn) => match && pilotRow[keyColumn].Equals(productionRow[keyColumn.Ordinal]))
                                                    && pilotRow.ItemArray.Where((item, index) => !columnsIgnored.Contains(index)).SequenceEqual(productionRow.ItemArray.Where((item, index) => !columnsIgnored.Contains(index)))
                                              select productionRow;

                // Modified rows in pilotTable : pilotRow PrimaryKey matches productionRow PrimaryKey BUT pilotRow does not match productionRow:
                var modifiedPilotRows = from pilotRow in pilotTable.AsEnumerable()
                                        from productionRow in productionTable.AsEnumerable()
                                        where pilotTable.PrimaryKey.Aggregate(true, (match, keyColumn) => match && pilotRow[keyColumn].Equals(productionRow[keyColumn.Ordinal]))
                                              && !pilotRow.ItemArray.Where((item, index) => !columnsIgnored.Contains(index)).SequenceEqual(productionRow.ItemArray.Where((item, index) => !columnsIgnored.Contains(index)))
                                        select pilotRow;

                // Modified rows in productionTable : pilotRow PrimaryKey matches productionRow PrimaryKey BUT pilotRow does not match productionRow:
                var modifiedProductionRows = from pilotRow in pilotTable.AsEnumerable()
                                             from productionRow in productionTable.AsEnumerable()
                                             where pilotTable.PrimaryKey.Aggregate(true, (match, keyColumn) => match && pilotRow[keyColumn].Equals(productionRow[keyColumn.Ordinal]))
                                                 && !pilotRow.ItemArray.Where((item, index) => !columnsIgnored.Contains(index)).SequenceEqual(productionRow.ItemArray.Where((item, index) => !columnsIgnored.Contains(index)))
                                             select productionRow;

                // Added rows : rows in pilotTable not modified AND not unchanged
                var addedRows = pilotTable.AsEnumerable().Except(modifiedPilotRows, DataRowComparer.Default).Except(unchangedPilotRows, DataRowComparer.Default);

                // Deleted rows : rows in productionTable not modified AND not unchanged
                var deletedRows = productionTable.AsEnumerable().Except(modifiedProductionRows, DataRowComparer.Default).Except(unchangedProductionRows, DataRowComparer.Default);

                var diffGramTable = pilotTable.Clone();
                diffGramTable.Columns.Add(Documenter.RowStateColumn);
                foreach (DataColumn column in pilotTable.Columns)
                {
                    if (!pilotTable.PrimaryKey.Contains(column))
                    {
                        diffGramTable.Columns.Add(Documenter.OldColumnPrefix + column.ColumnName);
                    }
                }

                // Populate unchanged rows
                foreach (var row in unchangedPilotRows)
                {
                    var newRow = diffGramTable.NewRow();
                    newRow[Documenter.RowStateColumn] = DataRowState.Unchanged;
                    foreach (DataColumn column in pilotTable.Columns)
                    {
                        newRow[column.ColumnName] = row[column.ColumnName];
                    }

                    diffGramTable.Rows.Add(newRow);
                    newRow.AcceptChanges();
                }

                // Populate modified rows
                foreach (var row in modifiedPilotRows)
                {
                    // Match the unmodified version of the row via the PrimaryKey
                    var matchInProductionTable = modifiedProductionRows.Where(mondifiedProductionRow => productionTable.PrimaryKey.Aggregate(true, (match, keyColumn) => match && mondifiedProductionRow[keyColumn].Equals(row[keyColumn.Ordinal]))).First();
                    var newRow = diffGramTable.NewRow();
                    newRow[Documenter.RowStateColumn] = DataRowState.Modified;

                    // Set the row with the original values
                    foreach (DataColumn column in pilotTable.Columns)
                    {
                        if (!pilotTable.PrimaryKey.Contains(column))
                        {
                            newRow[Documenter.OldColumnPrefix + column.ColumnName] = matchInProductionTable[column.ColumnName];
                        }
                    }

                    // Set the modified values
                    foreach (DataColumn column in pilotTable.Columns)
                    {
                        newRow[column.ColumnName] = row[column.ColumnName];
                    }

                    diffGramTable.Rows.Add(newRow);
                    newRow.AcceptChanges();
                }

                // Populate added rows
                foreach (var row in addedRows)
                {
                    var newRow = diffGramTable.NewRow();
                    newRow[Documenter.RowStateColumn] = DataRowState.Added;
                    foreach (DataColumn column in pilotTable.Columns)
                    {
                        newRow[column.ColumnName] = row[column.ColumnName];
                    }

                    diffGramTable.Rows.Add(newRow);
                    newRow.AcceptChanges();
                }

                // Populate deleted rows
                foreach (var row in deletedRows)
                {
                    if (row.RowError == Documenter.VanityRow)
                    {
                        break;
                    }

                    var newRow = diffGramTable.NewRow();
                    newRow[Documenter.RowStateColumn] = DataRowState.Deleted;
                    foreach (DataColumn column in pilotTable.Columns)
                    {
                        if (!pilotTable.PrimaryKey.Contains(column))
                        {
                            newRow[column.ColumnName] = row[column.ColumnName]; // save the value in the orginal column as well in case it's used for Sorting table
                            newRow[Documenter.OldColumnPrefix + column.ColumnName] = row[column.ColumnName];
                        }
                        else
                        {
                            newRow[column.ColumnName] = row[column.ColumnName];
                        }
                    }

                    diffGramTable.Rows.Add(newRow);
                    newRow.AcceptChanges();
                }

                return diffGramTable;
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Gets the print settings table for a configuration section.
        /// </summary>
        /// <returns>
        /// The print settings table for a configuration section.
        /// </returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "The method performs a time-consuming operation.")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "No good reason to call Dispose() on DataTable and DataColumn.")]
        protected static DataTable GetPrintTable()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var printTable = new DataTable("PrintSettings") { Locale = CultureInfo.InvariantCulture };

                var column1 = new DataColumn("TableIndex", typeof(int));
                var column2 = new DataColumn("ColumnIndex", typeof(int));
                var column3 = new DataColumn("Hidden", typeof(bool));
                var column4 = new DataColumn("SortOrder", typeof(int));
                var column5 = new DataColumn("BookmarkIndex", typeof(int));
                var column6 = new DataColumn("JumpToBookmarkIndex", typeof(int));
                var column7 = new DataColumn("ChangeIgnored", typeof(bool));

                printTable.Columns.Add(column1);
                printTable.Columns.Add(column2);
                printTable.Columns.Add(column3);
                printTable.Columns.Add(column4);
                printTable.Columns.Add(column5);
                printTable.Columns.Add(column6);
                printTable.Columns.Add(column7);
                printTable.PrimaryKey = new[] { column1, column2 };

                return printTable;
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Sorts the table.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="columns">The indexes of the columns on which to sort.</param>
        /// <returns>
        /// The sorted table.
        /// </returns>
        /// <exception cref="Exception">Max sort is on 7 columns</exception>
        protected static DataTable SortTable(DataTable table, int[] columns)
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                if (table == null)
                {
                    throw new ArgumentNullException("table");
                }

                var tableClone = table.Clone();

                IOrderedEnumerable<DataRow> rows;

                switch (columns.Length)
                {
                    case 1:
                        rows = table.Rows.Cast<DataRow>().OrderBy(row => row[columns[0]]);
                        break;
                    case 2:
                        rows = from row in table.Rows.Cast<DataRow>()
                               orderby row[columns[0]], row[columns[1]]
                               select row;
                        break;
                    case 3:
                        rows = from row in table.Rows.Cast<DataRow>()
                               orderby row[columns[0]], row[columns[1]], row[columns[2]]
                               select row;
                        break;
                    case 4:
                        rows = from row in table.Rows.Cast<DataRow>()
                               orderby row[columns[0]], row[columns[1]], row[columns[2]], row[columns[3]]
                               select row;
                        break;
                    case 5:
                        rows = from row in table.Rows.Cast<DataRow>()
                               orderby row[columns[0]], row[columns[1]], row[columns[2]], row[columns[3]], row[columns[4]]
                               select row;
                        break;
                    case 6:
                        rows = from row in table.Rows.Cast<DataRow>()
                               orderby row[columns[0]], row[columns[1]], row[columns[2]], row[columns[3]], row[columns[4]], row[columns[5]]
                               select row;
                        break;
                    case 7:
                        rows = from row in table.Rows.Cast<DataRow>()
                               orderby row[columns[0]], row[columns[1]], row[columns[2]], row[columns[3]], row[columns[4]], row[columns[5]], row[columns[6]]
                               select row;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("columns", "Columns length must be between 1 and 7");
                }

                foreach (var row in rows)
                {
                    tableClone.ImportRow(row);
                }

                tableClone.AcceptChanges();

                return tableClone;
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Adds the row to the table.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="row">The row.</param>
        protected static void AddRow(DataTable table, object row)
        {
            AddRow(table, row, false);
        }

        /// <summary>
        /// Adds the row to the table.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="row">The row.</param>
        /// <param name="vanityRow">if set to <c>true</c>, the row is not printed if it appears as a deleted row in the diffgram.</param>
        protected static void AddRow(DataTable table, object row, bool vanityRow)
        {
            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            if (row == null)
            {
                throw new ArgumentNullException("row");
            }

            try
            {
                var dataRow = row as DataRow;
                if (dataRow != null)
                {
                    table.Rows.Add(dataRow);
                }
                else
                {
                    var values = row as object[];
                    if (values != null)
                    {
                        dataRow = table.Rows.Add(values);
                    }
                    else
                    {
                        throw new ArgumentException("Parameter must be a DataRow or object[].", "row");
                    }
                }

                if (vanityRow)
                {
                    dataRow.RowError = Documenter.VanityRow;
                }
            }
            catch (DataException e)
            {
                Logger.Instance.WriteError(e.ToString());
            }
        }

        /// <summary>
        /// Writes the report header.
        /// </summary>
        /// <param name="htmlWriter">The HTML writer.</param>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected static void WriteReportHeader(HtmlTextWriter htmlWriter)
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                if (htmlWriter == null)
                {
                    throw new ArgumentNullException("htmlWriter");
                }

                htmlWriter.WriteFullBeginTag("head");

                #region style

                ////htmlWriter.WriteBeginTag("link");
                ////htmlWriter.WriteAttribute("rel", "stylesheet");
                ////htmlWriter.WriteAttribute("type", "text/css");
                ////htmlWriter.WriteAttribute("href", "documenter.css");
                ////htmlWriter.WriteLine(XhtmlTextWriter.SelfClosingTagEnd);

                htmlWriter.WriteBeginTag("style");
                htmlWriter.WriteAttribute("type", "text/css");
                htmlWriter.Write(HtmlTextWriter.TagRightChar);
                htmlWriter.Write(DocumenterResources.StyleSheet);
                htmlWriter.WriteEndTag("style");
                htmlWriter.WriteLine();

                #endregion style

                htmlWriter.WriteFullBeginTag("title");
                htmlWriter.Write("AAD Connect Config Documenter Report");
                htmlWriter.WriteEndTag("title");

                htmlWriter.WriteEndTag("head");
                htmlWriter.WriteLine();
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Writes the documenter information.
        /// </summary>
        /// <param name="htmlWriter">The HTML writer.</param>
        protected static void WriteDocumenterInfo(HtmlTextWriter htmlWriter)
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                if (htmlWriter == null)
                {
                    throw new ArgumentNullException("htmlWriter");
                }

                htmlWriter.WriteFullBeginTag("strong");
                htmlWriter.Write("Legend:");
                htmlWriter.WriteEndTag("strong");

                {
                    htmlWriter.WriteBeginTag("span");
                    htmlWriter.WriteAttribute("class", "Added");
                    htmlWriter.WriteLine(HtmlTextWriter.SelfClosingTagEnd);
                    htmlWriter.Write("Create ");
                    htmlWriter.WriteEndTag("span");

                    htmlWriter.WriteBeginTag("span");
                    htmlWriter.WriteAttribute("class", "Modified");
                    htmlWriter.WriteLine(HtmlTextWriter.SelfClosingTagEnd);
                    htmlWriter.Write("Update ");
                    htmlWriter.WriteEndTag("span");

                    htmlWriter.WriteBeginTag("span");
                    htmlWriter.WriteAttribute("class", "Deleted");
                    htmlWriter.WriteLine(HtmlTextWriter.SelfClosingTagEnd);
                    htmlWriter.Write("Delete ");
                    htmlWriter.WriteEndTag("span");

                    htmlWriter.WriteBeginTag("br");
                    htmlWriter.WriteLine(HtmlTextWriter.SelfClosingTagEnd);
                }

                htmlWriter.WriteFullBeginTag("strong");
                htmlWriter.Write("Documenter Version:");
                htmlWriter.WriteEndTag("strong");

                {
                    htmlWriter.WriteBeginTag("span");
                    htmlWriter.WriteAttribute("class", "Unchanged");
                    htmlWriter.WriteLine(HtmlTextWriter.SelfClosingTagEnd);
                    htmlWriter.Write(VersionInfo.Version);
                    htmlWriter.WriteEndTag("span");

                    htmlWriter.WriteBeginTag("br");
                    htmlWriter.WriteLine(HtmlTextWriter.SelfClosingTagEnd);
                }

                htmlWriter.WriteFullBeginTag("strong");
                htmlWriter.Write("Report Date:");
                htmlWriter.WriteEndTag("strong");

                {
                    htmlWriter.WriteBeginTag("span");
                    htmlWriter.WriteAttribute("class", "Unchanged");
                    htmlWriter.WriteLine(HtmlTextWriter.SelfClosingTagEnd);
                    htmlWriter.Write(DateTime.Now.ToString(CultureInfo.CurrentCulture));
                    htmlWriter.WriteEndTag("span");

                    htmlWriter.WriteBeginTag("br");
                    htmlWriter.WriteLine(HtmlTextWriter.SelfClosingTagEnd);
                }

                htmlWriter.WriteLine();
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Writes the bookmark location.
        /// </summary>
        /// <param name="htmlWriter">The HTML writer.</param>
        /// <param name="bookmark">The bookmark.</param>
        /// <param name="sectionGuid">The section unique identifier.</param>
        /// <param name="cellClass">The cell class.</param>
        protected static void WriteBookmarkLocation(HtmlTextWriter htmlWriter, string bookmark, string sectionGuid, string cellClass)
        {
            Logger.Instance.WriteMethodEntry("Bookmark: '{0}'. Section Guid: '{1}'. Cell Class: '{2}'.", bookmark, sectionGuid, cellClass);

            try
            {
                Documenter.WriteBookmarkLocation(htmlWriter, bookmark, bookmark, sectionGuid, cellClass);
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Bookmark: '{0}'. Section Guid: '{1}'. Cell Class: '{2}'.", bookmark, sectionGuid, cellClass);
            }
        }

        /// <summary>
        /// Writes the bookmark location.
        /// </summary>
        /// <param name="htmlWriter">The HTML writer.</param>
        /// <param name="bookmark">The bookmark.</param>
        /// <param name="displayText">The display text.</param>
        /// <param name="sectionGuid">The section unique identifier.</param>
        /// <param name="cellClass">The cell class.</param>
        protected static void WriteBookmarkLocation(HtmlTextWriter htmlWriter, string bookmark, string displayText, string sectionGuid, string cellClass)
        {
            Logger.Instance.WriteMethodEntry("Bookmark Code: '{0}'. Bookmark Text: '{1}'. Section Guid: '{2}'. Cell Class: '{3}'.", bookmark, displayText, sectionGuid, cellClass);

            try
            {
                if (htmlWriter == null)
                {
                    throw new ArgumentNullException("htmlWriter");
                }

                htmlWriter.WriteBeginTag("a");
                htmlWriter.WriteAttribute("class", cellClass);
                htmlWriter.WriteAttribute("name", Documenter.GetBookmarkCode(bookmark, sectionGuid));
                htmlWriter.Write(HtmlTextWriter.TagRightChar);
                htmlWriter.Write(displayText);
                htmlWriter.WriteEndTag("a");
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Bookmark Code: '{0}'. Bookmark Text: '{1}'. Section Guid: '{2}'. Cell Class: '{3}'.", bookmark, displayText, sectionGuid, cellClass);
            }
        }

        /// <summary>
        /// Writes the jump to bookmark location.
        /// </summary>
        /// <param name="htmlWriter">The HTML writer.</param>
        /// <param name="bookmark">The bookmark.</param>
        /// <param name="sectionGuid">The section unique identifier.</param>
        /// <param name="cellClass">The cell class.</param>
        protected static void WriteJumpToBookmarkLocation(HtmlTextWriter htmlWriter, string bookmark, string sectionGuid, string cellClass)
        {
            Logger.Instance.WriteMethodEntry("Bookmark: '{0}'. Section Guid: '{1}'. Cell Class: '{2}'.", bookmark, sectionGuid, cellClass);

            try
            {
                Documenter.WriteJumpToBookmarkLocation(htmlWriter, bookmark, bookmark, sectionGuid, cellClass);
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Bookmark: '{0}'. Section Guid: '{1}'. Cell Class: '{2}'.", bookmark, sectionGuid, cellClass);
            }
        }

        /// <summary>
        /// Writes the jump to bookmark location.
        /// </summary>
        /// <param name="htmlWriter">The HTML writer.</param>
        /// <param name="bookmark">The bookmark.</param>
        /// <param name="displayText">The display text.</param>
        /// <param name="sectionGuid">The section unique identifier.</param>
        /// <param name="cellClass">The cell class.</param>
        protected static void WriteJumpToBookmarkLocation(HtmlTextWriter htmlWriter, string bookmark, string displayText, string sectionGuid, string cellClass)
        {
            Logger.Instance.WriteMethodEntry("Bookmark Code: '{0}'. Bookmark Text: '{1}'. Section Guid: '{2}'. Cell Class: '{3}'.", bookmark, displayText, sectionGuid, cellClass);

            try
            {
                if (htmlWriter == null)
                {
                    throw new ArgumentNullException("htmlWriter");
                }

                htmlWriter.WriteBeginTag("a");
                htmlWriter.WriteAttribute("class", cellClass);
                htmlWriter.WriteAttribute("href", "#" + Documenter.GetBookmarkCode(bookmark, sectionGuid));
                htmlWriter.Write(HtmlTextWriter.TagRightChar);
                htmlWriter.Write(displayText);
                htmlWriter.WriteEndTag("a");
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Bookmark Code: '{0}'. Bookmark Text: '{1}'. Section Guid: '{2}'. Cell Class: '{3}'.", bookmark, displayText, sectionGuid, cellClass);
            }
        }

        /// <summary>
        /// Merges the ADSync configuration exports.
        /// </summary>
        /// <param name="configDirectory">The configuration directory.</param>
        /// <param name="pilotConfig">if set to <c>true</c>, indicates that this is a pilot configuration. Otherwise, this is a production configuration.</param>
        /// <returns>
        /// An <see cref="XElement" /> object representing the combined configuration XML object.
        /// </returns>
        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Xml.Linq.XElement.Parse(System.String)", Justification = "Template XML is not localizable.")]
        protected static XElement MergeConfigurationExports(string configDirectory, bool pilotConfig)
        {
            Logger.Instance.WriteMethodEntry("Config Directory: '{0}'. Pilot Config: '{1}'.", configDirectory, pilotConfig);

            try
            {
                var templateXml = string.Format(CultureInfo.InvariantCulture, "<Root><{0}><Connectors/><GlobalSettings/><SynchronizationRules/></{0}></Root>", pilotConfig ? "Pilot" : "Production");
                var configXml = XElement.Parse(templateXml);

                var connectors = configXml.XPathSelectElement("*//Connectors");
                foreach (var file in Directory.EnumerateFiles(configDirectory + "/Connectors", "*.xml"))
                {
                    connectors.Add(XElement.Load(file));
                }

                var globalSettings = configXml.XPathSelectElement("*//GlobalSettings");
                foreach (var file in Directory.EnumerateFiles(configDirectory + "/GlobalSettings", "*.xml"))
                {
                    globalSettings.Add(XElement.Load(file));
                }

                var synchronizationRules = configXml.XPathSelectElement("*//SynchronizationRules");
                foreach (var file in Directory.EnumerateFiles(configDirectory + "/SynchronizationRules", "*.xml"))
                {
                    synchronizationRules.Add(XElement.Load(file));
                }

                return configXml;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Config Directory: '{0}'. Pilot Config: '{1}'.", configDirectory, pilotConfig);
            }
        }

        /// <summary>
        /// Writes the rows.
        /// </summary>
        /// <param name="rows">The rows.</param>
        /// <param name="currentTableIndex">Index of the current table.</param>
        /// <param name="currentCellIndex">Index of the current cell.</param>
        [SuppressMessage("Microsoft.Usage", "CA2233:OperationsShouldNotOverflow", MessageId = "currentTableIndex+1", Justification = "Reviewed.")]
        protected void WriteRows(DataRow[] rows, int currentTableIndex, int currentCellIndex)
        {
            Logger.Instance.WriteMethodEntry("Current Table Index: '{0}'. Current Cell Index: '{1}'.", currentTableIndex, currentCellIndex);

            try
            {
                if (rows == null)
                {
                    throw new ArgumentNullException("rows");
                }

                var printTable = this.DiffgramDataSet.Tables["PrintSettings"];
                var maxCellCount = printTable.Select("Hidden = false").Count();

                var rowCount = rows.Length;

                for (var i = 0; i < rowCount; ++i)
                {
                    var row = rows[i];
                    var cellClass = (string)row[Documenter.RowStateColumn];

                    if (currentCellIndex == 0)
                    {
                        // Start the new row
                        this.ReportWriter.WriteBeginTag("tr");
                        this.ReportWriter.WriteAttribute("class", cellClass);
                        this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                    }

                    currentCellIndex = printTable.Select("Hidden = false AND TableIndex < " + currentTableIndex).Count();

                    var rowSpan = this.GetRowSpan(row, currentTableIndex) + 1;

                    var printColumns = printTable.Select("Hidden = false AND TableIndex = " + currentTableIndex).Select(rowX => rowX["ColumnIndex"]);
                    foreach (var column in row.Table.Columns.Cast<DataColumn>().Where(column => printColumns.Contains(column.Ordinal)))
                    {
                        this.WriteCell(row, column, rowSpan, currentTableIndex);
                        ++currentCellIndex;
                    }

                    // child rows
                    var childTableIndex = currentTableIndex + 1;
                    var dataRelationName = string.Format(CultureInfo.InvariantCulture, "DataRelation{0}{1}", childTableIndex, childTableIndex + 1);
                    var childRows = row.GetChildRows(dataRelationName);
                    var childRowsCount = childRows.Count();

                    if (childRowsCount == 0)
                    {
                        // complete the row if required
                        for (; currentCellIndex < maxCellCount; ++currentCellIndex)
                        {
                            this.ReportWriter.WriteBeginTag("td");
                            this.ReportWriter.WriteAttribute("class", cellClass);
                            this.ReportWriter.WriteAttribute("rowspan", "1");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("-");
                            this.ReportWriter.WriteEndTag("td");
                        }

                        this.ReportWriter.WriteEndTag("tr");
                        this.ReportWriter.WriteLine();
                    }
                    else
                    {
                        this.WriteRows(childRows, childTableIndex, currentCellIndex == maxCellCount ? 0 : currentCellIndex);
                    }
                }
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Current Table Index: '{0}'. Current Cell Index: '{1}'.", currentTableIndex, currentCellIndex);
            }
        }

        /// <summary>
        /// Gets the row span for the cells of the current row by counting all the predecessor rows.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="currentTableIndex">Index of the current table.</param>
        /// <returns>The number of all predecessor rows</returns>
        [SuppressMessage("Microsoft.Usage", "CA2233:OperationsShouldNotOverflow", MessageId = "currentTableIndex+1", Justification = "Reviewed.")]
        protected int GetRowSpan(DataRow row, int currentTableIndex)
        {
            Logger.Instance.WriteMethodEntry("Current Table Index: '{0}'.", currentTableIndex);

            try
            {
                if (row == null)
                {
                    throw new ArgumentNullException("row");
                }

                var rowSpan = 0;
                var childTableIndex = currentTableIndex + 1;
                var dataRelationName = string.Format(CultureInfo.InvariantCulture, "DataRelation{0}{1}", childTableIndex, childTableIndex + 1);
                var childRows = row.GetChildRows(dataRelationName);
                var childRowsCount = childRows.Count();
                for (var i = 0; i < childRowsCount; ++i)
                {
                    if (i > 0)
                    {
                        ++rowSpan;
                    }

                    rowSpan += this.GetRowSpan(childRows[i], currentTableIndex + 1);
                }

                return rowSpan;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Current Table Index: '{0}'.", currentTableIndex);
            }
        }

        /// <summary>
        /// Writes the cell.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <param name="rowSpan">The row span.</param>
        /// <param name="tableIndex">Index of the table.</param>
        protected void WriteCell(DataRow row, DataColumn column, int rowSpan, int tableIndex)
        {
            Logger.Instance.WriteMethodEntry("Row Span: '{0}'. Table Index: '{1}'.", rowSpan, tableIndex);

            try
            {
                if (row == null)
                {
                    throw new ArgumentNullException("row");
                }

                if (column == null)
                {
                    throw new ArgumentNullException("column");
                }

                var printTable = this.DiffgramDataSet.Tables["PrintSettings"];
                var rowFilter = string.Format(CultureInfo.InvariantCulture, "TableIndex = {0} AND ColumnIndex = {1} AND (BookmarkIndex <> -1 OR JumpToBookmarkIndex <> -1)", tableIndex, column.Ordinal);
                var printRow = printTable.Select(rowFilter).FirstOrDefault();
                var bookmarkIndex = printRow != null && (int)printRow["BookmarkIndex"] != -1 ? printRow["BookmarkIndex"] : null;
                var jumpToBookmarkIndex = printRow != null && (int)printRow["JumpToBookmarkIndex"] != -1 ? printRow["JumpToBookmarkIndex"] : null;

                var cellClass = (string)row[Documenter.RowStateColumn];

                this.ReportWriter.WriteBeginTag("td");
                this.ReportWriter.WriteAttribute("class", cellClass);
                this.ReportWriter.WriteAttribute("rowspan", rowSpan.ToString(CultureInfo.InvariantCulture));
                this.ReportWriter.Write(HtmlTextWriter.TagRightChar);

                if (column.Table.PrimaryKey.Contains(column))
                {
                    var text = Convert.ToString(row[column.ColumnName], CultureInfo.InvariantCulture);
                    if (bookmarkIndex != null)
                    {
                        Documenter.WriteBookmarkLocation(this.ReportWriter, text, Convert.ToString(row[(int)bookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                    }
                    else if (jumpToBookmarkIndex != null)
                    {
                        Documenter.WriteJumpToBookmarkLocation(this.ReportWriter, text, Convert.ToString(row[(int)jumpToBookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                    }
                    else
                    {
                        this.ReportWriter.Write(text);
                    }
                }
                else
                {
                    var rowState = (DataRowState)Enum.Parse(typeof(DataRowState), row[Documenter.RowStateColumn].ToString());

                    switch (rowState)
                    {
                        case DataRowState.Modified:
                            {
                                var oldText = Convert.ToString(row[Documenter.OldColumnPrefix + column.ColumnName], CultureInfo.InvariantCulture);
                                var text = Convert.ToString(row[column.ColumnName], CultureInfo.InvariantCulture);

                                if (oldText != text)
                                {
                                    cellClass = DataRowState.Deleted.ToString();
                                    this.ReportWriter.WriteBeginTag("span");
                                    this.ReportWriter.WriteAttribute("class", cellClass);
                                    this.ReportWriter.Write(HtmlTextWriter.TagRightChar);

                                    if (bookmarkIndex != null)
                                    {
                                        Documenter.WriteBookmarkLocation(this.ReportWriter, oldText, Convert.ToString(row[(int)bookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                                    }
                                    else if (jumpToBookmarkIndex != null)
                                    {
                                        Documenter.WriteJumpToBookmarkLocation(this.ReportWriter, oldText, Convert.ToString(row[(int)jumpToBookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                                    }
                                    else
                                    {
                                        this.ReportWriter.Write(oldText);
                                    }
                                }

                                this.ReportWriter.WriteEndTag("span");
                                this.ReportWriter.WriteBeginTag("span");
                                this.ReportWriter.WriteAttribute("class", DataRowState.Modified.ToString());
                                this.ReportWriter.Write(HtmlTextWriter.TagRightChar);

                                if (bookmarkIndex != null)
                                {
                                    Documenter.WriteBookmarkLocation(this.ReportWriter, text, Convert.ToString(row[(int)bookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                                }
                                else if (jumpToBookmarkIndex != null)
                                {
                                    Documenter.WriteJumpToBookmarkLocation(this.ReportWriter, text, Convert.ToString(row[(int)jumpToBookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                                }
                                else
                                {
                                    this.ReportWriter.Write(text);
                                }

                                this.ReportWriter.WriteEndTag("span");
                                break;
                            }

                        case DataRowState.Deleted:
                            {
                                cellClass = DataRowState.Deleted.ToString();
                                var text = Convert.ToString(row[Documenter.OldColumnPrefix + column.ColumnName], CultureInfo.InvariantCulture);
                                if (bookmarkIndex != null)
                                {
                                    Documenter.WriteBookmarkLocation(this.ReportWriter, text, Convert.ToString(row[(int)bookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                                }
                                else if (jumpToBookmarkIndex != null)
                                {
                                    Documenter.WriteJumpToBookmarkLocation(this.ReportWriter, text, Convert.ToString(row[(int)jumpToBookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                                }
                                else
                                {
                                    this.ReportWriter.Write(text);
                                }

                                break;
                            }

                        default:
                            {
                                var text = Convert.ToString(row[column.ColumnName], CultureInfo.InvariantCulture);
                                if (bookmarkIndex != null)
                                {
                                    Documenter.WriteBookmarkLocation(this.ReportWriter, text, Convert.ToString(row[(int)bookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                                }
                                else if (jumpToBookmarkIndex != null)
                                {
                                    Documenter.WriteJumpToBookmarkLocation(this.ReportWriter, text, Convert.ToString(row[(int)jumpToBookmarkIndex], CultureInfo.InvariantCulture), cellClass);
                                }
                                else
                                {
                                    this.ReportWriter.Write(text);
                                }

                                break;
                            }
                    }
                }

                this.ReportWriter.WriteEndTag("td");
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Row Span: '{0}'. Table Index: '{1}'.", rowSpan, tableIndex);
            }
        }

        #region Simple Settings Sections

        /// <summary>
        /// Creates the simple settings data sets.
        /// </summary>
        /// <param name="columnCount">The column count.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "No good reason to call Dispose() on DataTable and DataColumn.")]
        protected void CreateSimpleSettingsDataSets(int columnCount)
        {
            Logger.Instance.WriteMethodEntry("Column Count: '{0}'.", columnCount);

            try
            {
                var table = new DataTable("SimpleSettings") { Locale = CultureInfo.InvariantCulture };

                for (var i = 0; i < columnCount; ++i)
                {
                    table.Columns.Add(new DataColumn("Column" + (i + 1)));
                }

                table.PrimaryKey = new[] { table.Columns[0] };

                this.PilotDataSet = new DataSet("SimpleSettings") { Locale = CultureInfo.InvariantCulture };
                this.PilotDataSet.Tables.Add(table);

                this.ProductionDataSet = this.PilotDataSet.Clone();

                var printTable = this.GetSimpleSettingsPrintTable(columnCount);
                this.PilotDataSet.Tables.Add(printTable);
                this.ProductionDataSet.Tables.Add(printTable.Copy());
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Column Count: '{0}'.", columnCount);
            }
        }

        /// <summary>
        /// Gets the simple settings print table.
        /// </summary>
        /// <param name="columnCount">The column count.</param>
        /// <returns>
        /// The simple settings print table.
        /// </returns>
        protected DataTable GetSimpleSettingsPrintTable(int columnCount)
        {
            Logger.Instance.WriteMethodEntry("Column Count: '{0}'.", columnCount);

            try
            {
                var printTable = Documenter.GetPrintTable();

                for (var i = 0; i < columnCount; ++i)
                {
                    printTable.Rows.Add((new OrderedDictionary { { "TableIndex", 0 }, { "ColumnIndex", i }, { "Hidden", false }, { "SortOrder", (i == 0) ? 0 : -1 }, { "BookmarkIndex", -1 }, { "JumpToBookmarkIndex", -1 }, { "ChangeIgnored", false } }).Values.Cast<object>().ToArray());
                }

                printTable.AcceptChanges();

                return printTable;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Column Count: '{0}'.", columnCount);
            }
        }

        /// <summary>
        /// Creates the simple settings difference gram.
        /// </summary>
        protected void CreateSimpleSettingsDiffgram()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                this.DiffgramDataSet = Documenter.GetDiffgram(this.PilotDataSet, this.ProductionDataSet);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Simple Settings Sections

        #region Simple Ordered Settings Sections

        /// <summary>
        /// Creates the simple ordered settings data sets.
        /// </summary>
        /// <param name="columnCount">The column count.</param>
        protected void CreateSimpleOrderedSettingsDataSets(int columnCount)
        {
            Logger.Instance.WriteMethodEntry("Column Count: '{0}'.", columnCount);

            try
            {
                this.CreateSimpleOrderedSettingsDataSets(columnCount, 2);
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Column Count: '{0}'.", columnCount);
            }
        }

        /// <summary>
        /// Creates the simple ordered settings data sets
        /// </summary>
        /// <param name="columnCount">The column count.</param>
        /// <param name="keyCount">The key count.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "No good reason to call Dispose() on DataTable and DataColumn.")]
        protected void CreateSimpleOrderedSettingsDataSets(int columnCount, int keyCount)
        {
            Logger.Instance.WriteMethodEntry("Column Count: '{0}'. Primary Key Count: '{1}'.", columnCount, keyCount);

            try
            {
                var table = new DataTable("SimpleOrderedSettings") { Locale = CultureInfo.InvariantCulture };

                table.Columns.Add(new DataColumn("Column1", typeof(int)));

                for (var i = 1; i < columnCount; ++i)
                {
                    table.Columns.Add(new DataColumn("Column" + (i + 1)));
                }

                var primaryKey = new List<DataColumn>(keyCount);
                for (var i = 0; i < keyCount; ++i)
                {
                    primaryKey.Add(table.Columns[i]);
                }

                table.PrimaryKey = primaryKey.ToArray();

                this.PilotDataSet = new DataSet("SimpleOrderedSettings") { Locale = CultureInfo.InvariantCulture };
                this.PilotDataSet.Tables.Add(table);

                this.ProductionDataSet = this.PilotDataSet.Clone();

                var printTable = this.GetSimpleOrderedSettingsPrintTable(columnCount);
                this.PilotDataSet.Tables.Add(printTable);
                this.ProductionDataSet.Tables.Add(printTable.Copy());
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Column Count: '{0}'. Primary Key Count: '{1}'.", columnCount, keyCount);
            }
        }

        /// <summary>
        /// Gets the simple ordered settings print table.
        /// </summary>
        /// <param name="columnCount">The column count.</param>
        /// <returns>
        /// The simple ordered settings print table.
        /// </returns>
        protected DataTable GetSimpleOrderedSettingsPrintTable(int columnCount)
        {
            Logger.Instance.WriteMethodEntry("Column Count: '{0}'.", columnCount);

            try
            {
                var printTable = Documenter.GetPrintTable();

                for (var i = 0; i < columnCount; ++i)
                {
                    printTable.Rows.Add((new OrderedDictionary { { "TableIndex", 0 }, { "ColumnIndex", i }, { "Hidden", i == 0 }, { "SortOrder", (i == 0) ? 0 : -1 }, { "BookmarkIndex", -1 }, { "JumpToBookmarkIndex", -1 }, { "ChangeIgnored", false } }).Values.Cast<object>().ToArray());
                }

                printTable.AcceptChanges();

                return printTable;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Column Count: '{0}'.", columnCount);
            }
        }

        /// <summary>
        /// Creates the simple ordered settings difference gram.
        /// </summary>
        protected void CreateSimpleOrderedSettingsDiffgram()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                this.DiffgramDataSet = Documenter.GetDiffgram(this.PilotDataSet, this.ProductionDataSet);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Simple Ordered Settings Sections
    }
}