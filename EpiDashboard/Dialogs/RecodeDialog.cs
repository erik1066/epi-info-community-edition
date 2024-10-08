﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using EpiDashboard;
using Epi;
using Epi.Core;
using Epi.Fields;
using Epi.Windows;
using Epi.Windows.Dialogs;
using EpiDashboard.Rules;

namespace EpiDashboard.Dialogs
{
    public partial class RecodeDialog : DialogBase
    {
        #region Private Members
        private DashboardHelper dashboardHelper;
        private Rule_Recode recodeRule;
        private string sourceColumnName;
        private bool editMode;

        private const string COL_FROM = "From";
        private const string COL_TO = "To";
        private const string COL_REPRESENTATION = "Representation";
        #endregion // Private Members

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        public RecodeDialog(DashboardHelper dashboardHelper)
        {
            this.dashboardHelper = dashboardHelper;
            this.editMode = false;            
            InitializeComponent();
            FillComboBoxes();
            this.checkboxMaintainSortOrder.Checked = true;
            this.checkboxUseWildcards.Checked = false;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public RecodeDialog(DashboardHelper dashboardHelper, Rule_Recode rule)
        {
            this.dashboardHelper = dashboardHelper;
            this.editMode = true;
            this.recodeRule = rule;
            InitializeComponent();

            FillComboBoxes();

            this.txtDestinationField.Text = rule.DestinationColumnName;
            this.cbxSourceField.SelectedItem = rule.SourceColumnName;
            this.checkboxMaintainSortOrder.Checked = rule.ShouldMaintainSortOrder;
            this.checkboxUseWildcards.Checked = rule.ShouldUseWildcards;
            this.txtElseValue.Text = rule.ElseValue;

            // TODO: Find better way to do this
            switch (rule.DestinationColumnType)
            {
                case "System.SByte":
                case "System.Byte":
                case "System.Boolean":
                    this.cbxFieldType.SelectedItem = "Yes/No";
                    break;
                case "System.String":
                    this.cbxFieldType.SelectedItem = "Text";
                    break;
                case "System.Single":
                case "System.Double":
                case "System.Decimal":
                case "System.Int32":
                case "System.Int16":
                    this.cbxFieldType.SelectedItem = "Numeric";
                    break;
            }

            this.cbxFieldType.Enabled = false;

            if (rule.RecodeInputTable.Columns.Count == 3)
            {
                rule.RecodeInputTable.Columns[0].ColumnName = COL_FROM;
                rule.RecodeInputTable.Columns[1].ColumnName = COL_TO;
                rule.RecodeInputTable.Columns[2].ColumnName = COL_REPRESENTATION;
            }
            else if (rule.RecodeInputTable.Columns.Count == 2)
            {
                rule.RecodeInputTable.Columns[0].ColumnName = COL_FROM;
                rule.RecodeInputTable.Columns[1].ColumnName = COL_REPRESENTATION;
            }

            dataGridViewRecode.DataSource = rule.RecodeInputTable;

			dataGridViewRecode.CellValidating += DataGridViewRecode_CellValidating;
        }

		private void DataGridViewRecode_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
		{
            if (dataGridViewRecode.IsCurrentCellDirty)
            {
                var editCell = dataGridViewRecode[e.ColumnIndex, e.RowIndex];
                DateTime cellParse = new DateTime();

                if(this.recodeRule.SourceColumnType == typeof(DateTime).ToString())
                {
                    if(e.ColumnIndex < (dataGridViewRecode.ColumnCount - 1))
                    {
                        if (false == DateTime.TryParse(editCell.EditedFormattedValue.ToString(), System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out cellParse))
                        {
                            editCell.ErrorText = SharedStrings.DASHBOARD_ERROR_PARSE_TO_DATETIME;
                        }
                        else
                        {
                            editCell.ErrorText = null;
                        }
                    }
                }
            }
        }
		#endregion // Constructors

		#region Public Properties
		/// <summary>
		/// Gets the dashboard helper object attached to this control
		/// </summary>
		public DashboardHelper DashboardHelper
        {
            get
            {
                return this.dashboardHelper;
            }
        }

        /// <summary>
        /// Gets the Epi.View associated with this control
        /// </summary>
        public Epi.View View
        {
            get
            {
                return this.DashboardHelper.View;
            }
        }

        /// <summary>
        /// Gets the recoding rule generated by this dialog
        /// </summary>
        public Rule_Recode RecodeRule
        {
            get
            {
                return this.recodeRule;
            }
            private set
            {
                this.recodeRule = value;
            }
        }

        /// <summary>
        /// Gets the destination field type
        /// </summary>
        public DashboardVariableType DestinationFieldType
        {
            get
            {
                if (cbxFieldType.SelectedIndex >= 0)
                {
                    switch (cbxFieldType.SelectedItem.ToString())
                    {
                        case "Yes/No":
                            return DashboardVariableType.YesNo;
                        case "Numeric":
                            return DashboardVariableType.Numeric;
                        case "Text":
                            return DashboardVariableType.Text;
                        default:
                            return DashboardVariableType.None;
                    }
                }
                else
                {
                    return DashboardVariableType.None;
                }
            }
        }

        /// <summary>
        /// Gets the recode values generated by this dialog
        /// </summary>
        public DataTable RecodeTable
        {
            get
            {
                return ((DataTable)this.dataGridViewRecode.DataSource);
            }
        }
        #endregion // Public Properties

        #region Private Methods
        /// <summary>
        /// Fills the combo boxes on this dialog
        /// </summary>
        private void FillComboBoxes()
        {
            txtDestinationField.Text = string.Empty;
            cbxSourceField.Items.Clear();
            cbxFieldType.Items.Clear();            

            List<string> fieldNames = new List<string>();

            if (dashboardHelper.IsUsingEpiProject)
            {
                ColumnDataType columnDataType = ColumnDataType.Boolean | ColumnDataType.Numeric | ColumnDataType.Text | ColumnDataType.DateTime | ColumnDataType.UserDefined;
                fieldNames = dashboardHelper.GetFieldsAsList(columnDataType);
            }
            else
            {
                ColumnDataType columnDataType = ColumnDataType.Boolean | ColumnDataType.Numeric | ColumnDataType.Text | ColumnDataType.DateTime | ColumnDataType.UserDefined;
                fieldNames = dashboardHelper.GetFieldsAsList(columnDataType);
            }

            fieldNames.Sort();
            cbxSourceField.DataSource = fieldNames;
            
            cbxFieldType.Items.Add("Text");
            cbxFieldType.Items.Add("Numeric");
            cbxFieldType.Items.Add("Yes/No");
            cbxFieldType.SelectedIndex = 0;

            if (editMode)
            {
                txtDestinationField.Enabled = false;
            }
            else
            {
                txtDestinationField.Enabled = true;
            }
        }

        /// <summary>
        /// Fills the data grid view with the appropriate columns
        /// </summary>
        private void FillDataGrid()
        {
            DataTable dt = new DataTable("recode");
            Configuration config = dashboardHelper.Config;
            
            dataGridViewRecode.Columns.Clear();
            dataGridViewRecode.AllowUserToAddRows = true;

            if (dashboardHelper.IsUsingEpiProject && View.Fields.Contains(sourceColumnName))
            {
                Field sourceField = dashboardHelper.View.Fields[sourceColumnName];

                if (sourceField is NumberField || sourceField is OptionField)
                {
                    DataColumn col1 = new DataColumn(COL_FROM, typeof(string));
                    DataColumn col2 = new DataColumn(COL_TO, typeof(string));
                    dt.Columns.Add(col1);
                    dt.Columns.Add(col2);
                }
                else if (sourceField is DateTimeField)
                {
                    DataColumn col1 = new DataColumn(COL_FROM, typeof(DateTime));
                    DataColumn col2 = new DataColumn(COL_TO, typeof(DateTime));
                    dt.Columns.Add(col1);
                    dt.Columns.Add(col2);
                }
                else
                {
                    DataColumn col1 = new DataColumn(COL_FROM, typeof(string));
                    dt.Columns.Add(col1);
                }
            }
            else
            {
                if (dashboardHelper.IsColumnNumeric(sourceColumnName))
                {
                    DataColumn col1 = new DataColumn(COL_FROM, typeof(string));
                    DataColumn col2 = new DataColumn(COL_TO, typeof(string));
                    dt.Columns.Add(col1);
                    dt.Columns.Add(col2);
                }
                else if (dashboardHelper.IsColumnDateTime(sourceColumnName))
                {
                    DataColumn col1 = new DataColumn(COL_FROM, typeof(DateTime));
                    DataColumn col2 = new DataColumn(COL_TO, typeof(DateTime));
                    dt.Columns.Add(col1);
                    dt.Columns.Add(col2);
                }
                else
                {
                    DataColumn col1 = new DataColumn(COL_FROM, typeof(string));
                    dt.Columns.Add(col1);
                }
            }

            DataColumn col3 = new DataColumn(COL_REPRESENTATION, typeof(string));

            bool isTableBasedDropDown = false;

            if (dashboardHelper.IsUsingEpiProject && View.Fields.Contains(sourceColumnName))
            {
                Field field = dashboardHelper.View.Fields[sourceColumnName];
                if (field is TableBasedDropDownField)
                {
                    isTableBasedDropDown = true;
                }
            }

            if (DestinationFieldType.Equals(DashboardVariableType.Numeric))
            {                
                dt.Columns.Add(col3);
            }
            else if (DestinationFieldType.Equals(DashboardVariableType.YesNo) && !(isTableBasedDropDown))
            {
                col3.ReadOnly = true;

                dataGridViewRecode.AllowUserToAddRows = true;

                dt.Columns.Add(col3);
                if (dt.Columns.Count == 2)
                {
                    dt.Rows.Add(null, config.Settings.RepresentationOfYes);
                    dt.Rows.Add(null, config.Settings.RepresentationOfNo);
                }
                else
                {
                    dt.Rows.Add(null, null, config.Settings.RepresentationOfYes);
                    dt.Rows.Add(null, null, config.Settings.RepresentationOfNo);
                }
            }
            else
            {   
                dt.Columns.Add(col3);
            }

            if (isTableBasedDropDown && dashboardHelper.IsUsingEpiProject)
            {
                Field sourceField = View.Fields[sourceColumnName];
                DataTable codeDataTable = ((TableBasedDropDownField)sourceField).GetSourceData();
                Dictionary<string, string> fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (System.Data.DataRow row in codeDataTable.Rows)
                {
                    string Key = row[((TableBasedDropDownField)sourceField).TextColumnName.Trim()].ToString();

                    if (sourceField is DDLFieldOfCommentLegal)
                    {
                        int dashLocation = Key.IndexOf('-');
                        Key = Key.Substring(0, dashLocation);
                    }

                    if (!fieldValues.ContainsKey(Key))
                    {
                        fieldValues.Add(Key, Key);
                    }
                }

                int i = 0;
                foreach (KeyValuePair<string, string> kvp in fieldValues)
                {
                    if (kvp.Key.Length > 0)
                    {
                        if (DestinationFieldType.Equals(DashboardVariableType.YesNo))
                        {
                            if (i % 2 == 1)
                            {
                                dt.Rows.Add(kvp.Key, config.Settings.RepresentationOfYes);
                            }
                            else
                            {
                                dt.Rows.Add(kvp.Key, config.Settings.RepresentationOfNo);
                            }
                            i++;
                        }
                        else
                        {
                            dt.Rows.Add(kvp.Key, null);
                        }
                    }
                }                
            }
            else if(dt.Rows.Count <= 0)
            {
                if (dt.Columns.Count == 2)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        dt.Rows.Add(null, null);
                    }
                }
                else
                {
                    for (int i = 0; i < 10; i++)
                    {
                        dt.Rows.Add(null, null, null);
                    }
                }
            }

            dataGridViewRecode.DataSource = dt;            
        }

        private void EnableDisableFillRanges()
        {
            if (dashboardHelper.IsColumnNumeric(sourceColumnName) && DestinationFieldType.Equals(DashboardVariableType.Text))
            {
                btnFillRanges.Enabled = true;
            }
            else
            {
                btnFillRanges.Enabled = false;
            }
        }
        #endregion //Private Methods

        private void cbxSourceField_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbxSourceField.SelectedIndex >= 0 && dashboardHelper != null)
            {
                if (dashboardHelper.IsUsingEpiProject)
                {
                    ColumnDataType columnDataType = ColumnDataType.Boolean | ColumnDataType.Numeric | ColumnDataType.Text | ColumnDataType.DateTime | ColumnDataType.UserDefined;
                    foreach (string str in dashboardHelper.GetFieldsAsList(columnDataType))
                    {
                        if (str.ToLowerInvariant().Equals(cbxSourceField.SelectedItem.ToString().ToLowerInvariant().Trim()))
                        {
                            sourceColumnName = str;
                        }
                    }
                }
                else
                {
                    ColumnDataType columnDataType = ColumnDataType.Boolean | ColumnDataType.Numeric | ColumnDataType.Text | ColumnDataType.DateTime | ColumnDataType.UserDefined;
                    foreach (string str in dashboardHelper.GetFieldsAsList(columnDataType))
                    {
                        if (str.ToLowerInvariant().Equals(cbxSourceField.SelectedItem.ToString().ToLowerInvariant().Trim()))
                        {
                            sourceColumnName = str;
                        }
                    }
                }

                if (!editMode)
                {
                    txtDestinationField.Text = cbxSourceField.SelectedItem.ToString() + "_RECODED";
                }
                FillDataGrid();
            }
            else
            {
                dataGridViewRecode.Rows.Clear();
                dataGridViewRecode.Columns.Clear();
            }

            EnableDisableFillRanges();
        }

        private void cbxFieldType_SelectedIndexChanged(object sender, EventArgs e)
        {
            FillDataGrid();
            EnableDisableFillRanges();            
        }

        private void btnFillRanges_Click(object sender, EventArgs e)
        {
            FillRangesDialog fillRangesDialog = new FillRangesDialog();
            DialogResult result = fillRangesDialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                int startValue = fillRangesDialog.StartValue;
                int endValue = fillRangesDialog.EndValue;
                int rangeValue = fillRangesDialog.RangeValue;

                DataTable rangeTable = ((DataTable)dataGridViewRecode.DataSource);
                rangeTable.Rows.Clear();

                rangeTable.Rows.Add("LOVALUE", startValue.ToString(), "LOVALUE - <" + startValue.ToString());

                for (int i = startValue; i < endValue; i = i + rangeValue)
                {
                    string lowerBound = i.ToString();
                    string upperBound = (i + rangeValue).ToString();

                    if ((i + rangeValue) > endValue)
                    {
                        upperBound = endValue.ToString();
                    }

                    rangeTable.Rows.Add(lowerBound, upperBound, lowerBound + " - <" + upperBound);
                }

                rangeTable.Rows.Add(endValue.ToString(), "HIVALUE", endValue.ToString() + " - < HIVALUE");
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtDestinationField.Text))
            {
                MsgBox.ShowError("Destination field is blank.");
                this.DialogResult = DialogResult.None;
                return;
            }

            if (cbxFieldType.SelectedIndex == 1 && cbxFieldType.Text == "Numeric")
            {
                foreach (DataRow row in RecodeTable.Rows)
                {
                    string textValue = row["Representation"].ToString();
                    double value;
                    bool success = double.TryParse(textValue, out value);
                    if (!success && !string.IsNullOrEmpty(textValue))
                    {
                        MsgBox.ShowError("The destination field type has been defined as numeric, but the destination values are not valid numbers.");
                        this.DialogResult = DialogResult.None;
                        return;
                    }
                }
            }

            if (!editMode)
            {
                ColumnDataType columnDataType = ColumnDataType.Boolean | ColumnDataType.Numeric | ColumnDataType.Text;
                foreach (string s in dashboardHelper.GetFieldsAsList(columnDataType))
                {
                    if (txtDestinationField.Text.ToLowerInvariant().Equals(s.ToLowerInvariant()))
                    {
                        MsgBox.ShowError("Destination field name already exists as a column in this data set. Please use another name.");
                        this.DialogResult = DialogResult.None;
                        return;
                    }
                }

                foreach (IDashboardRule rule in dashboardHelper.Rules)
                {
                    if (rule is DataAssignmentRule)
                    {
                        DataAssignmentRule assignmentRule = rule as DataAssignmentRule;
                        if (txtDestinationField.Text.ToLowerInvariant().Equals(assignmentRule.DestinationColumnName.ToLowerInvariant()))
                        {
                            MsgBox.ShowError("Destination field name already exists as a defined field with recoded values. Please use another field name.");
                            this.DialogResult = DialogResult.None;
                            return;
                        }
                    }
                }
            }

            string friendlyRule = "Recode the values in " + sourceColumnName + " to " + txtDestinationField.Text + "";
            string sourceColumnType = dashboardHelper.GetColumnType(sourceColumnName);
            RecodeRule = new Rule_Recode(this.DashboardHelper, friendlyRule, sourceColumnName, sourceColumnType, txtDestinationField.Text, DestinationFieldType, RecodeTable, txtElseValue.Text, checkboxMaintainSortOrder.Checked, checkboxUseWildcards.Checked);
        }
    }
}
