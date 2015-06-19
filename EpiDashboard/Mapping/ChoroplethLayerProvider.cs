﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Toolkit;
using ESRI.ArcGIS.Client.Bing;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Symbols;
using ESRI.ArcGIS.Client.Tasks;
using Epi;
using Epi.Data;
using EpiDashboard.Mapping.ShapeFileReader;

namespace EpiDashboard.Mapping
{
    public class ChoroplethLayerProvider : ILayerProvider
    {
        #region Choropleth

        Map _myMap;
        DashboardHelper _dashboardHelper;
        string _shapeKey;
        string _dataKey;
        string _valueField;
        Guid _layerId;
        List<SolidColorBrush> _colors;
        int _classCount;
        int _colorShadeIndex = 0;
        int _lastGeneratedClassCount = 0;
        string[,] _rangeValues = new string[,]{{"",""},{"",""},{"",""},{"",""},{"",""},{"",""},{"",""},{"","" },{"",""},{"",""}};

        public ChoroplethLayerProvider(Map myMap)
        {
            _myMap = myMap;
            _layerId = Guid.NewGuid();
        }

        public string[,] RangeValue
        {
            get { return _rangeValues; }
            set { _rangeValues = value; }
        }

        public struct ThematicItem
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string CalcField { get; set; }
            public double Min { get; set; }
            public double Max { get; set; }
            public string MinName { get; set; }
            public string MaxName { get; set; }
            public List<double> RangeStarts { get; set; }
        }

        public void MoveUp()
        {
            Layer layer = _myMap.Layers[_layerId.ToString()];
            int currentIndex = _myMap.Layers.IndexOf(layer);
            if (currentIndex < _myMap.Layers.Count - 1)
            {
                _myMap.Layers.Remove(layer);
                _myMap.Layers.Insert(currentIndex + 1, layer);
            }
        }

        public void MoveDown()
        {
            Layer layer = _myMap.Layers[_layerId.ToString()];
            int currentIndex = _myMap.Layers.IndexOf(layer);
            if (currentIndex > 1)
            {
                _myMap.Layers.Remove(layer);
                _myMap.Layers.Insert(currentIndex - 1, layer);
            }
        }

        private Color GetColorAtPoint(Rectangle theRec, Point thePoint)
        {    
            LinearGradientBrush br = (LinearGradientBrush)theRec.Fill;
            double y3 = thePoint.Y; double x3 = thePoint.X;
            double x1 = br.StartPoint.X * theRec.Width;
            double y1 = br.StartPoint.Y * theRec.Height;
            Point p1 = new Point(x1, y1); 
            double x2 = br.EndPoint.X * theRec.Width;
            double y2 = br.EndPoint.Y * theRec.Height;
            Point p2 = new Point(x2, y2);  
            Point p4 = new Point(); 
            if (y1 == y2) 
            { p4 = new Point(x3, y1); }
            else if (x1 == x2) 
            { p4 = new Point(x1, y3); }
            else 
            {
                double m = (y2 - y1) / (x2 - x1);
                double m2 = -1 / m;
                double b = y1 - m * x1;
                double c = y3 - m2 * x3;
                double x4 = (c - b) / (m - m2);
                double y4 = m * x4 + b; p4 = new Point(x4, y4);
            }     
            double d4 = dist(p4, p1, p2);
            double d2 = dist(p2, p1, p2);
            double x = d4 / d2;     
            double max = br.GradientStops.Max(n => n.Offset);
            if (x > max) { x = max; }
            double min = br.GradientStops.Min(n => n.Offset);
            if (x < min) { x = min; }     
            GradientStop gs0 = br.GradientStops.Where(n => n.Offset <= x).OrderBy(n => n.Offset).Last();
            GradientStop gs1 = br.GradientStops.Where(n => n.Offset >= x).OrderBy(n => n.Offset).First();
            float y = 0f;
            if (gs0.Offset != gs1.Offset)
            { y = (float)((x - gs0.Offset) / (gs1.Offset - gs0.Offset)); }     
            Color cx = new Color();
            if (br.ColorInterpolationMode == ColorInterpolationMode.ScRgbLinearInterpolation)
            {
                float aVal = (gs1.Color.ScA - gs0.Color.ScA) * y + gs0.Color.ScA;
                float rVal = (gs1.Color.ScR - gs0.Color.ScR) * y + gs0.Color.ScR;
                float gVal = (gs1.Color.ScG - gs0.Color.ScG) * y + gs0.Color.ScG;
                float bVal = (gs1.Color.ScB - gs0.Color.ScB) * y + gs0.Color.ScB;
                cx = Color.FromScRgb(aVal, rVal, gVal, bVal);
            }
            else
            {
                byte aVal = (byte)((gs1.Color.A - gs0.Color.A) * y + gs0.Color.A);
                byte rVal = (byte)((gs1.Color.R - gs0.Color.R) * y + gs0.Color.R);
                byte gVal = (byte)((gs1.Color.G - gs0.Color.G) * y + gs0.Color.G);
                byte bVal = (byte)((gs1.Color.B - gs0.Color.B) * y + gs0.Color.B);
                cx = Color.FromArgb(aVal, rVal, gVal, bVal);
            }
            return cx;
        }
        private double dist(Point px, Point po, Point pf)
        {
            double d = Math.Sqrt((px.Y - po.Y) * (px.Y - po.Y) + (px.X - po.X) * (px.X - po.X)); 
            if (((px.Y < po.Y) && (pf.Y > po.Y)) || ((px.Y > po.Y) && (pf.Y < po.Y)) || ((px.Y == po.Y) && (px.X < po.X) && (pf.X > po.X)) || ((px.Y == po.Y) && (px.X > po.X) && (pf.X < po.X))) 
            { 
                d = -d; 
            } 
            return d;
        }

        private void CreateColorList1(Color lowColor, Color highColor)
        {
            //LinearGradientBrush gradientBrush = new LinearGradientBrush(highColor, lowColor, 0);
            //Rectangle temp = new Rectangle();
            //temp.Width = 256;
            //temp.Height = 256;
            //temp.Fill = gradientBrush;
            
            //ColorList = new List<List<SolidColorBrush>>();

            //List<SolidColorBrush> BlueShades = new List<SolidColorBrush>();

            //int rgbFactor = 255 / classCount;
            //Random rnd = new Random();
            //for (int j = 0; j < 256; j = j + rgbFactor)
            //{
            //    Color color = Color.FromRgb((byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255));// GetColorAtPoint(temp, new Point(j, j));
            //    color.A = 0xF0;
            //    BlueShades.Add(new SolidColorBrush(color));
            //}

            //ColorList.Add(BlueShades);

            //foreach (List<SolidColorBrush> brushList in ColorList)
            //{
            //    brushList.Reverse();
            //}

            //_lastGeneratedClassCount = classCount;
        }

        private int GetRangeIndex(double val, List<double> ranges)
        {
            int limit;
            limit = ranges.Count < _classCount ? ranges.Count : _classCount;

            int index = limit - 1;
            for (int r = 0; r < limit - 1; r++)
            {
                if (val >= ranges[r] && val < ranges[r + 1]) index = r;
            }
            return index;
        }

        public struct Values
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Value { get; set; }
        }

        //private void ColorBlendCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //if (ColorBlendCombo != null)
        //{
        //    _colorShadeIndex = ColorBlendCombo.SelectedIndex;
        //    if (loadedData != null)
        //    {
        //        SetShapeRangeValues();
        //    }
        //}
        //}

        //private void ClassCountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //if (ClassCountCombo != null)
        //{
        //    ComboBoxItem item = ClassCountCombo.SelectedItem as ComboBoxItem;
        //    _classCount = Convert.ToInt32(item.Content);
        //    if (loadedData != null)
        //    {
        //        SetShapeRangeValues();
        //    }
        //}
        //}

        public object[] LoadShapeFile(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                //Get the file info objects for the SHP and the DBF file selected by the user
                FileInfo shapeFile = new FileInfo(fileName);
                FileInfo dbfFile = new FileInfo(fileName.ToLower().Replace(".shp", ".dbf"));
                if (!dbfFile.Exists)
                {
                    System.Windows.MessageBox.Show("Associated DBF file not found");
                    return null;
                }

                //Read the SHP and DBF files into the ShapeFileReader
                ShapeFileReader.ShapeFile shapeFileReader = new ShapeFileReader.ShapeFile();
                if (shapeFile != null && dbfFile != null)
                {
                    shapeFileReader.Read(shapeFile, dbfFile);
                }
                else
                {
                    System.Windows.MessageBox.Show("Please select a SP and a DBF file to proceed.");
                    return null;
                }

                GraphicsLayer graphicsLayer = _myMap.Layers[_layerId.ToString()] as GraphicsLayer;
                if (graphicsLayer == null)
                {
                    graphicsLayer = new GraphicsLayer();
                    graphicsLayer.ID = _layerId.ToString();
                    _myMap.Layers.Add(graphicsLayer);
                }

                int recCount = shapeFileReader.Records.Count;
                int rgbFactor = 255 / recCount;
                int counter = 0;
                foreach (ShapeFileReader.ShapeFileRecord record in shapeFileReader.Records)
                {
                    Graphic graphic = record.ToGraphic();
                    if (graphic != null)
                    {
                        graphic.Symbol = GetFillSymbol(new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)));
                        graphicsLayer.Graphics.Add(graphic);
                    }
                    counter += rgbFactor;
                }
                if (graphicsLayer.FullExtent == null)
                {
                    Envelope shapeFileExtent = shapeFileReader.GetExtent();
                    if (shapeFileExtent.SpatialReference == null)
                    {
                        _myMap.Extent = shapeFileExtent;
                    }
                    else
                    {
                        if (shapeFileExtent.SpatialReference.WKID == 4326)
                        {
                            _myMap.Extent = new Envelope(ESRI.ArcGIS.Client.Bing.Transform.GeographicToWebMercator(new MapPoint(shapeFileExtent.XMin, shapeFileExtent.YMin)), ESRI.ArcGIS.Client.Bing.Transform.GeographicToWebMercator(new MapPoint(shapeFileExtent.XMax, shapeFileExtent.YMax)));
                        }
                    }
                }
                else
                {
                    _myMap.Extent = graphicsLayer.FullExtent;
                }
                graphicsLayer.RenderingMode = GraphicsLayerRenderingMode.Static;
                return new object[] { fileName, graphicsLayer.Graphics[0].Attributes };
            }
            else return null;
        }

        public object[] LoadShapeFile()
        {
            //Create the dialog allowing the user to select the "*.shp" and the "*.dbf" files
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "ESRI Shapefiles (*.shp)|*.shp";
            //ofd.Multiselect = true;

            if (ofd.ShowDialog().Value)
            {
                //Get the file info objects for the SHP and the DBF file selected by the user
                FileInfo shapeFile = new FileInfo(ofd.FileName);
                FileInfo dbfFile = new FileInfo(ofd.FileName.ToLower().Replace(".shp", ".dbf"));
                if (!dbfFile.Exists)
                {
                    System.Windows.MessageBox.Show("Associated DBF file not found");
                    return null;
                }
                foreach (string fname in ofd.FileNames)
                {
                    FileInfo fi = new FileInfo(fname);
                    if (fi.Extension.ToLower() == ".shp")
                    {
                        shapeFile = fi;
                    }
                    if (fi.Extension.ToLower() == ".dbf")
                    {
                        dbfFile = fi;
                    }
                }

                //Read the SHP and DBF files into the ShapeFileReader
                ShapeFileReader.ShapeFile shapeFileReader = new ShapeFileReader.ShapeFile();
                if (shapeFile != null && dbfFile != null)
                {
                    shapeFileReader.Read(shapeFile, dbfFile);
                }
                else
                {
                    System.Windows.MessageBox.Show("Please select a SP and a DBF file to proceed.");
                    return null;
                }

                GraphicsLayer graphicsLayer = _myMap.Layers[_layerId.ToString()] as GraphicsLayer;
                if (graphicsLayer == null)
                {
                    graphicsLayer = new GraphicsLayer();
                    graphicsLayer.ID = _layerId.ToString();
                    _myMap.Layers.Add(graphicsLayer);
                }

                int recCount = shapeFileReader.Records.Count;
                int rgbFactor = 255 / recCount;
                int counter = 0;
                //int minPoints = shapeFileReader.Records.Min(record => record.NumberOfPoints);
                //double meanPoints = shapeFileReader.Records.Average(record => record.NumberOfPoints);
                foreach (ShapeFileReader.ShapeFileRecord record in shapeFileReader.Records)
                {
                    Graphic graphic = record.ToGraphic();
                    if (graphic != null)
                    {
                        graphic.Symbol = GetFillSymbol(new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)));
                        graphicsLayer.Graphics.Add(graphic);
                    }
                    counter += rgbFactor;
                }
                Envelope shapeFileExtent = shapeFileReader.GetExtent();
                if (shapeFileExtent.SpatialReference == null)
                {
                    _myMap.Extent = shapeFileExtent;
                }
                else
                {
                    if (shapeFileExtent.SpatialReference.WKID == 4326)
                    {
                        _myMap.Extent = new Envelope(ESRI.ArcGIS.Client.Bing.Transform.GeographicToWebMercator(new MapPoint(shapeFileExtent.XMin, shapeFileExtent.YMin)), ESRI.ArcGIS.Client.Bing.Transform.GeographicToWebMercator(new MapPoint(shapeFileExtent.XMax, shapeFileExtent.YMax)));
                    }
                }
                graphicsLayer.RenderingMode = GraphicsLayerRenderingMode.Static;
                return new object[] { ofd.FileName, graphicsLayer.Graphics[0].Attributes };
            }
            else return null;
        }

        public SimpleFillSymbol GetFillSymbol(SolidColorBrush brush)
        {
            SimpleFillSymbol symbol = new SimpleFillSymbol();
            symbol.Fill = brush;
            symbol.BorderBrush = new SolidColorBrush(Colors.Gray);
            symbol.BorderThickness = 1;
            return symbol;
        }



        public void Refresh()
        {
            if (_dashboardHelper != null)
            {
                SetShapeRangeValues(_dashboardHelper, _shapeKey, _dataKey, _valueField, _colors, _classCount);
            }
        }

        public void SetShapeRangeValues(DashboardHelper dashboardHelper, string shapeKey, string dataKey, string valueField, List<SolidColorBrush> colors, int classCount)
        {
            try
            {
                _classCount = classCount;
                _dashboardHelper = dashboardHelper;
                _shapeKey = shapeKey;
                _dataKey = dataKey;
                _valueField = valueField;
                _colors = colors;

                List<string> columnNames = new List<string>();
                if (dashboardHelper.IsUsingEpiProject)
                {
                    columnNames.Add("UniqueKey");
                }
                columnNames.Add(valueField);
                columnNames.Add(dataKey);

                DataTable loadedData;

                if (valueField.Equals("{Record Count}"))
                {
                    GadgetParameters gadgetOptions = new GadgetParameters();
                    gadgetOptions.MainVariableName = dataKey;

                    Dictionary<string, string> inputVariableList = new Dictionary<string, string>();
                    inputVariableList.Add("freqvar", dataKey);
                    inputVariableList.Add("allvalues", "false");
                    inputVariableList.Add("showconflimits", "false");
                    inputVariableList.Add("showcumulativepercent", "false");
                    inputVariableList.Add("includemissing", "false");
                    inputVariableList.Add("maxrows", "500");

                    gadgetOptions.InputVariableList = inputVariableList;
                    loadedData = dashboardHelper.GenerateFrequencyTable(gadgetOptions).First().Key;
                    foreach (DataRow dr in loadedData.Rows)
                    {
                        dr[0] = dr[0].ToString().Trim();
                    }
                    valueField = "freq";
                }
                else
                {
                    loadedData = dashboardHelper.GenerateTable(columnNames);
                }


                GraphicsLayer graphicsLayer = _myMap.Layers[_layerId.ToString()] as GraphicsLayer;
                ThematicItem thematicItem = GetThematicItem(shapeKey, dataKey, valueField, classCount, loadedData, graphicsLayer);

                if (graphicsLayer.Graphics != null && graphicsLayer.Graphics.Count > 0)
                {

                    for (int i = 0; i < graphicsLayer.Graphics.Count; i++)
                    {
                        Graphic graphicFeature = graphicsLayer.Graphics[i];

                        //string filterExpression = dataKey + " = '" + graphicFeature.Attributes[shapeKey].ToString().Trim() + "'";
                        string filterExpression = "";
                        if (dataKey.Contains(" ") || dataKey.Contains("$") || dataKey.Contains("#"))
                            filterExpression += "[";
                        filterExpression += dataKey;
                        if (dataKey.Contains(" ") || dataKey.Contains("$") || dataKey.Contains("#"))
                            filterExpression += "]";
                        filterExpression += " = '" + graphicFeature.Attributes[shapeKey].ToString().Replace("'", "''").Trim() + "'";

                        double graphicValue = Double.PositiveInfinity;
                        try
                        {
                            graphicValue = Convert.ToDouble(loadedData.Select(filterExpression)[0][valueField]);
                        }
                        catch (Exception)
                        {
                            graphicValue = Double.PositiveInfinity;
                        }

                        int brushIndex = GetRangeIndex(graphicValue, thematicItem.RangeStarts);

                        SimpleFillSymbol symbol = new SimpleFillSymbol()
                        {
                            Fill = graphicValue == Double.PositiveInfinity ? new SolidColorBrush(Colors.Transparent) : colors[brushIndex],
                            BorderBrush = new SolidColorBrush(Colors.Black),
                            BorderThickness = 1
                        };

                        graphicFeature.Symbol = symbol;

                        TextBlock t = new TextBlock();
                        t.Background = Brushes.White;
                        if (graphicValue == Double.PositiveInfinity)
                        {
                            t.Text = graphicFeature.Attributes[shapeKey].ToString().Trim() + " : No Data";
                        }
                        else
                        {
                            t.Text = graphicFeature.Attributes[shapeKey].ToString().Trim() + " : " + graphicValue.ToString();
                        }
                        t.FontSize = 14;
                        Border border = new Border();
                        border.BorderThickness = new Thickness(1);
                        Panel panel = new StackPanel();
                        panel.Children.Add(t);
                        border.Child = panel;

                        graphicFeature.MapTip = border;
                    }

                }


                if (LegendStackPanel == null)
                {
                    LegendStackPanel = new StackPanel();
                }
                LegendStackPanel.Children.Clear();

                System.Windows.Controls.ListBox legendList = new System.Windows.Controls.ListBox();
                legendList.Margin = new Thickness(5);
                legendList.Background = Brushes.White;// new LinearGradientBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x7F, 0xFF, 0xFF, 0xFF), 45);
                legendList.BorderBrush = Brushes.Black;
                legendList.BorderThickness = new Thickness(3);
                //LegendTitle.Text = thematicItem.Description;

                for (int c = 0; c < classCount; c++)
                {
                    try
                    {
                        Rectangle swatchRect = new Rectangle()
                        {
                            Width = 20,
                            Height = 20,
                            Stroke = new SolidColorBrush(Colors.Black),
                            Fill = colors[c]
                        };

                        TextBlock classTextBlock = new TextBlock();

                        if (c == 0)
                        {
                            if (thematicItem.RangeStarts[1] == thematicItem.Min)
                                classTextBlock.Text = String.Format("  Exactly {0}", Math.Round(thematicItem.RangeStarts[1], 2));
                            else
                                classTextBlock.Text = String.Format("  Less than {0}", Math.Round(thematicItem.RangeStarts[1], 2));
                        }
                        else if (c == classCount - 1)
                            classTextBlock.Text = String.Format("  {0} and above", Math.Round(thematicItem.RangeStarts[c], 2));
                        else if (thematicItem.RangeStarts.Count <= c + 1)
                        {
                            classTextBlock.Text = String.Format("  {0} and above", Math.Round(thematicItem.RangeStarts[c], 2));
                        }
                        // Middle classifications
                        else
                        {
                            if (thematicItem.RangeStarts[c] == thematicItem.RangeStarts[c + 1])
                                classTextBlock.Text = String.Format("  Exactly {0}", Math.Round(thematicItem.RangeStarts[c], 2));
                            else
                                classTextBlock.Text = String.Format("  {0} to {1}", Math.Round(thematicItem.RangeStarts[c], 2), Math.Round(thematicItem.RangeStarts[c + 1], 2));
                        }

                        StackPanel classStackPanel = new StackPanel();
                        classStackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                        classStackPanel.Children.Add(swatchRect);
                        classStackPanel.Children.Add(classTextBlock);

                        legendList.Items.Add(classStackPanel);
                        if (thematicItem.RangeStarts.Count <= c + 1)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }

                TextBlock minTextBlock = new TextBlock();
                StackPanel minStackPanel = new StackPanel();
                minStackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                minTextBlock.Text = thematicItem.MinName != null ? String.Format("Min: {0} ({1})", thematicItem.Min, thematicItem.MinName.Trim()) : String.Format("Min: {0} ({1})", thematicItem.Min, string.Empty);
                minStackPanel.Children.Add(minTextBlock);
                legendList.Items.Add(minStackPanel);

                TextBlock maxTextBlock = new TextBlock();
                StackPanel maxStackPanel = new StackPanel();
                maxStackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                maxTextBlock.Text = thematicItem.MaxName != null ? String.Format("Max: {0} ({1})", thematicItem.Max, thematicItem.MaxName.Trim()) : String.Format("Max: {0} ({1})", thematicItem.Max, string.Empty);
                maxStackPanel.Children.Add(maxTextBlock);
                legendList.Items.Add(maxStackPanel);

                LegendStackPanel.Children.Add(legendList);

            }
            catch (Exception ex)
            {
            }
        }

        public List<object> GetRangeValues()
        {
            return new List<object>();
        }

        public ThematicItem GetThematicItem(string shapeKey, string dataKey, string valueField, int classCount, DataTable loadedData, GraphicsLayer graphicsLayer)
        {
            ThematicItem thematicItem = new ThematicItem() { Name = dataKey, Description = dataKey, CalcField = "" };
            List<double> valueList = new List<double>();
            for (int i = 0; i < graphicsLayer.Graphics.Count; i++)
            {
                Graphic graphicFeature = graphicsLayer.Graphics[i];
                //string filterExpression = dataKey + " = '" + graphicFeature.Attributes[shapeKey].ToString().Trim() + "'";
                string filterExpression = "";
                if (dataKey.Contains(" ") || dataKey.Contains("$") || dataKey.Contains("#"))
                    filterExpression += "[";
                filterExpression += dataKey;
                if (dataKey.Contains(" ") || dataKey.Contains("$") || dataKey.Contains("#"))
                    filterExpression += "]";
                filterExpression += " = '" + graphicFeature.Attributes[shapeKey].ToString().Replace("'", "''").Trim() + "'";

                double graphicValue = Double.PositiveInfinity;
                try
                {
                    graphicValue = Convert.ToDouble(loadedData.Select(filterExpression)[0][valueField]);
                }
                catch (Exception ex)
                {
                    graphicValue = Double.PositiveInfinity;
                }

                string graphicName = graphicFeature.Attributes[shapeKey].ToString();

                if (i == 0)
                {
                    thematicItem.Min = Double.PositiveInfinity;
                    thematicItem.Max = Double.NegativeInfinity;
                    thematicItem.MinName = string.Empty;
                    thematicItem.MaxName = string.Empty;
                }
                else
                {
                    if (graphicValue < thematicItem.Min) { thematicItem.Min = graphicValue; thematicItem.MinName = graphicName; }
                    if (graphicValue > thematicItem.Max && graphicValue != Double.PositiveInfinity) { thematicItem.Max = graphicValue; thematicItem.MaxName = graphicName; }
                }

                if (graphicValue < Double.PositiveInfinity)
                {
                    valueList.Add(graphicValue);
                }
            }
            thematicItem.RangeStarts = new List<double>();

            double totalRange = thematicItem.Max - thematicItem.Min;
            double portion = totalRange / classCount;

            thematicItem.RangeStarts.Add(thematicItem.Min);
            double startRangeValue = thematicItem.Min;
            IEnumerable<double> valueEnumerator =
            from aValue in valueList
            orderby aValue
            select aValue;

            int increment = Convert.ToInt32(Math.Round((double)valueList.Count / (double)classCount));
            for (int i = increment; i < valueList.Count; i += increment)
            {
                double value = valueEnumerator.ElementAt(i);
                if (value < thematicItem.Min)
                    value = thematicItem.Min;
                thematicItem.RangeStarts.Add(value);
            }
            return thematicItem;
        }

        private StackPanel legendStackPanel;

        public StackPanel LegendStackPanel
        {
            get
            {
                return legendStackPanel;
            }
            set
            {
                legendStackPanel = value;
            }
        }

        #endregion

        #region ILayerProvider Members

        public void CloseLayer()
        {
            GraphicsLayer graphicsLayer = _myMap.Layers[_layerId.ToString()] as GraphicsLayer;
            if (graphicsLayer != null)
            {
                _myMap.Layers.Remove(graphicsLayer);
                if (legendStackPanel != null)
                {
                    legendStackPanel.Children.Clear();
                }
            }
        }

        #endregion
    }
}
