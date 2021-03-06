﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Castle.ActiveRecord;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using GIS.Common;
using LibCommon;
using LibEntity;

namespace GIS.SpecialGraphic
{
    public partial class CollapsePillarsEntering : Form
    {
        private string _errorMsg;


        /// <summary>
        ///     构造方法
        /// </summary>
        public CollapsePillarsEntering()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     构造方法
        /// </summary>
        public CollapsePillarsEntering(IPointCollection pointCollection)
        {
            InitializeComponent();

            dgrdvCoordinate.RowCount = pointCollection.PointCount;
            for (int i = 0; i < pointCollection.PointCount - 1; i++)
            {
                dgrdvCoordinate[0, i].Value = pointCollection.Point[i].X;
                dgrdvCoordinate[1, i].Value = pointCollection.Point[i].Y;
                if (pointCollection.Point[i].Z.ToString(CultureInfo.InvariantCulture) == "非数字" ||
                    pointCollection.Point[i].Z.ToString(CultureInfo.InvariantCulture) == "NaN")
                    dgrdvCoordinate[2, i].Value = 0;
                else
                    dgrdvCoordinate[2, i].Value = pointCollection.Point[i].Z;
            }
        }

        /// <summary>
        ///     构造方法
        /// </summary>
        /// <params name="collapsePillar"></params>
        public CollapsePillarsEntering(CollapsePillar collapsePillar)
        {
            InitializeComponent();
            using (new SessionScope())
            {
                collapsePillar = CollapsePillar.Find(collapsePillar.id);
                txtCollapsePillarsName.Text = collapsePillar.name;
                if (collapsePillar.xtype == "1")
                    radioBtnS.Checked = true;
                txtDescribe.Text = collapsePillar.discribe;
                foreach (var t in collapsePillar.collapse_pillar_points)
                {
                    dgrdvCoordinate.Rows.Add(t.coordinate_x,
                        t.coordinate_y,
                        t.coordinate_z);
                }
            }
        }

        /// <summary>
        ///     取消按钮事件
        /// </summary>
        /// <params name="sender"></params>
        /// <params name="e"></params>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            //关闭窗体
            Close();
        }

        /// <summary>
        ///     提交按钮
        /// </summary>
        /// <params name="sender"></params>
        /// <params name="e"></params>
        private void btnSubmit_Click(object sender, EventArgs e)
        {
            var collapsePillar =
                CollapsePillar.FindAllByProperty("name", txtCollapsePillarsName.Text).FirstOrDefault();
            if (collapsePillar == null)
            {
                collapsePillar = new CollapsePillar
                {
                    name = txtCollapsePillarsName.Text,
                    discribe = txtDescribe.Text,
                    xtype = radioBtnX.Checked ? "0" : "1",
                    bid = IdGenerator.NewBindingId()
                };
            }
            else
            {
                collapsePillar.name = txtCollapsePillarsName.Text;
                collapsePillar.discribe = txtDescribe.Text;
                collapsePillar.xtype = radioBtnX.Checked ? "0" : "1";
            }

            //实体赋值
            //去除无用空行
            for (int i = 0; i < dgrdvCoordinate.RowCount - 1; i++)
            {
                if (dgrdvCoordinate.Rows[i].Cells[0].Value == null &&
                    dgrdvCoordinate.Rows[i].Cells[1].Value == null &&
                    dgrdvCoordinate.Rows[i].Cells[2].Value == null)
                {
                    dgrdvCoordinate.Rows.RemoveAt(i);
                }
            }
            collapsePillar.Save();

            //添加关键点
            List<CollapsePillarPoint> collapsePillarPoints = new List<CollapsePillarPoint>();
            for (int i = 0; i < dgrdvCoordinate.RowCount - 1; i++)
            {

                var collapsePillarPoint = new CollapsePillarPoint
                {
                    coordinate_x = Convert.ToDouble(dgrdvCoordinate[0, i].Value),
                    coordinate_y = Convert.ToDouble(dgrdvCoordinate[1, i].Value),
                    coordinate_z = Convert.ToDouble(dgrdvCoordinate[2, i].Value),
                    bid = IdGenerator.NewBindingId(),
                    collapse_pillar = collapsePillar
                };
                collapsePillarPoints.Add(collapsePillarPoint);
                collapsePillarPoint.Save();
            }

            ModifyXlz(collapsePillarPoints, collapsePillar.bid);
            DialogResult = DialogResult.OK;
        }

        /// <summary>
        ///     显示行号
        /// </summary>
        /// <params name="sender"></params>
        /// <params name="e"></params>
        private void dgrdvCoordinate_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var rectangle = new Rectangle(e.RowBounds.Location.X,
                e.RowBounds.Location.Y, dgrdvCoordinate.RowHeadersWidth - 4, e.RowBounds.Height);

            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(),
                dgrdvCoordinate.RowHeadersDefaultCellStyle.Font, rectangle,
                dgrdvCoordinate.RowHeadersDefaultCellStyle.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void dgrdvCoordinate_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex != dgrdvCoordinate.Rows.Count - 1 && e.ColumnIndex == 3 &&
                Alert.Confirm("确认要删除吗？"))
            {
                if (e.ColumnIndex == 3)
                {
                    dgrdvCoordinate.Rows.RemoveAt(e.RowIndex);
                }
            }
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            try
            {
                var open = new OpenFileDialog { Filter = @"陷落柱数据(*.txt)|*.txt" };
                if (open.ShowDialog(this) == DialogResult.Cancel)
                    return;
                string filename = open.FileName;
                string[] file = File.ReadAllLines(filename);
                dgrdvCoordinate.RowCount = file.Length;
                if (open.SafeFileName != null) txtCollapsePillarsName.Text = open.SafeFileName.Split('.')[0];
                for (int i = 0; i < file.Length; i++)
                {
                    dgrdvCoordinate[0, i].Value = file[i].Split(',')[0];
                    dgrdvCoordinate[1, i].Value = file[i].Split(',')[1];
                    //dgrdvCoordinate[2, i].Value = file[i].Split(',')[2];
                    dgrdvCoordinate[2, i].Value = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #region 根据关键点绘制陷落柱

        /// <summary>
        ///     修改陷落柱图元
        /// </summary>
        /// <params name="lstCollapsePillarsEntKeyPts"></params>
        /// <params name="sCollapseId"></params>
        private void ModifyXlz(List<CollapsePillarPoint> lstCollapsePillarsEntKeyPts, string sCollapseId)
        {
            //1.获得当前编辑图层
            var drawspecial = new DrawSpecialCommon();
            const string sLayerAliasName = LayerNames.DEFALUT_COLLAPSE_PILLAR_1; //“陷落柱_1”图层
            IFeatureLayer featureLayer = drawspecial.GetFeatureLayerByName(sLayerAliasName);
            if (featureLayer == null)
            {
                MessageBox.Show(@"未找到" + sLayerAliasName + @"图层,无法修改陷落柱图元。");
                return;
            }

            //2.删除原来图元，重新绘制新图元
            bool bIsDeleteOldFeature = DataEditCommon.DeleteFeatureByBId(featureLayer, sCollapseId);
            if (bIsDeleteOldFeature)
            {
                //绘制图元
                DrawXlz(lstCollapsePillarsEntKeyPts, sCollapseId);
            }
        }


        private void DrawXlz(List<CollapsePillarPoint> lstCollapsePillarsEntKeyPts, string sCollapseId)
        {
            ILayer mPCurrentLayer = DataEditCommon.GetLayerByName(DataEditCommon.g_pMap,
                LayerNames.LAYER_ALIAS_MR_XianLuoZhu1);
            var pFeatureLayer = mPCurrentLayer as IFeatureLayer;
            INewBezierCurveFeedback pBezier = new NewBezierCurveFeedbackClass();
            IPoint pt;
            IPolyline polyline = new PolylineClass();
            for (int i = 0; i < lstCollapsePillarsEntKeyPts.Count; i++)
            {
                pt = new PointClass();
                var mZAware = (IZAware)pt;
                mZAware.ZAware = true;

                pt.X = lstCollapsePillarsEntKeyPts[i].coordinate_x;
                pt.Y = lstCollapsePillarsEntKeyPts[i].coordinate_y;
                pt.Z = lstCollapsePillarsEntKeyPts[i].coordinate_z;
                if (i == 0)
                {
                    pBezier.Start(pt);
                }
                else if (i == lstCollapsePillarsEntKeyPts.Count - 1)
                {
                    pBezier.AddPoint(pt);
                    pt = new PointClass();
                    var zZAware = (IZAware)pt;
                    zZAware.ZAware = true;
                    pt.X = lstCollapsePillarsEntKeyPts[0].coordinate_x;
                    pt.Y = lstCollapsePillarsEntKeyPts[0].coordinate_y;
                    pt.Z = lstCollapsePillarsEntKeyPts[0].coordinate_z;
                    pBezier.AddPoint(pt);
                    polyline = pBezier.Stop();
                }
                else
                    pBezier.AddPoint(pt);
            }
            //polyline = (IPolyline)geo;
            var pSegmentCollection = polyline as ISegmentCollection;
            if (pSegmentCollection != null)
            {
                for (int i = 0; i < pSegmentCollection.SegmentCount; i++)
                {
                    pt = new PointClass();
                    var mZAware = (IZAware)pt;
                    mZAware.ZAware = true;

                    pt.X = lstCollapsePillarsEntKeyPts[i].coordinate_x;
                    pt.Y = lstCollapsePillarsEntKeyPts[i].coordinate_y;
                    pt.Z = lstCollapsePillarsEntKeyPts[i].coordinate_z;


                    IPoint pt1 = new PointClass();
                    mZAware = (IZAware)pt1;
                    mZAware.ZAware = true;
                    if (i == pSegmentCollection.SegmentCount - 1)
                    {
                        pt1.X = lstCollapsePillarsEntKeyPts[0].coordinate_x;
                        pt1.Y = lstCollapsePillarsEntKeyPts[0].coordinate_y;
                        pt1.Z = lstCollapsePillarsEntKeyPts[0].coordinate_z;

                        pSegmentCollection.Segment[i].FromPoint = pt;
                        pSegmentCollection.Segment[i].ToPoint = pt1;
                    }
                    else
                    {
                        pt1.X = lstCollapsePillarsEntKeyPts[i + 1].coordinate_x;
                        pt1.Y = lstCollapsePillarsEntKeyPts[i + 1].coordinate_y;
                        pt1.Z = lstCollapsePillarsEntKeyPts[i + 1].coordinate_z;

                        pSegmentCollection.Segment[i].FromPoint = pt;
                        pSegmentCollection.Segment[i].ToPoint = pt1;
                    }
                }
            }
            polyline = pSegmentCollection as IPolyline;
            //polyline = DataEditCommon.PDFX(polyline, "Bezier");

            IPolygon pPolygon = DataEditCommon.PolylineToPolygon(polyline);
            var list = new List<ziduan>
            {
                new ziduan("COLLAPSE_PILLAR_NAME", lstCollapsePillarsEntKeyPts.First().collapse_pillar.name),
                new ziduan("BID", sCollapseId),
                radioBtnX.Checked ? new ziduan("XTYPE", "0") : new ziduan("XTYPE", "1")
            };
            IFeature pFeature = DataEditCommon.CreateNewFeature(pFeatureLayer, pPolygon, list);
            if (pFeature != null)
            {
                MyMapHelp.Jump(pFeature.Shape);
                DataEditCommon.g_pMyMapCtrl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewBackground, null, null);
            }

            #region 暂时无用

            //string sTempFolderPath = System.Windows.Forms.Application.StartupPath + "\\TempFolder";

            /////1.将关键点坐标存储到临时文件中
            //string sPtsCoordinateTxtPath = sTempFolderPath + "\\PtsCoordinate.txt";
            //bool bIsWrite = WritePtsInfo2Txt(lstCollapsePillarsEntKeyPts, sPtsCoordinateTxtPath);
            //if (!bIsWrite) return;

            /////2.读取点坐标文件拟合生成陷落柱，仿照等值线
            /////步骤：点文件生成点要素层→转为Raster→提取等值线
            //Geoprocessor GP = new Geoprocessor();
            //string featureOut = sTempFolderPath + "\\KeyPts.shp";
            //DrawContours.ConvertASCIIDescretePoint2FeatureClass(GP, sPtsCoordinateTxtPath, featureOut);//点文件生成点要素层

            //string sRasterOut = sTempFolderPath + "\\Raster";
            //DrawContours.ConvertFeatureCls2Raster(GP, featureOut, sRasterOut);//要素层→Raster

            //string sR2Contour = sTempFolderPath + "\\Contour.shp";
            //double douElevation = 0.5;//等高距0.5
            //DrawContours.SplineRasterToContour(GP, sRasterOut, sR2Contour, douElevation);//提取等值线（即为拟合的陷落柱）

            /////3.复制生成的等值线（即为拟合的陷落柱）要素到陷落柱图层
            /////3.1 获得源图层
            //IFeatureLayer sourceFeaLayer = new FeatureLayerClass();
            //string sourcefeatureClassName = "Contour.shp";
            //IFeatureClass featureClass =PointsFit2Polyline.GetFeatureClassFromShapefileOnDisk(sTempFolderPath, sourcefeatureClassName);//获得等值线（即为拟合的陷落柱）图层

            //if (featureClass == null) return;
            //sourceFeaLayer.FeatureClass = featureClass;


            /////3.2 获得当前编辑图层(目标图层)
            //DrawSpecialCommon drawspecial = new DrawSpecialCommon();
            //string sLayerAliasName = LibCommon.LibLayerNames.DEFALUT_COLLAPSE_PILLAR;//“陷落柱_1”图层
            //IFeatureLayer featureLayer = drawspecial.GetFeatureLayerByName(sLayerAliasName);
            //if (featureLayer == null)
            //{
            //    MessageBox.Show("未找到" + sLayerAliasName + "图层,无法绘制陷落柱图元。");
            //    return;
            //}

            /////3.3 复制要素
            //PointsFit2Polyline.CopyFeature(sourceFeaLayer, featureLayer, sCollapseID);

            #endregion
        }

        #endregion

        private void btnMultImport_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                RestoreDirectory = true,
                Filter = @"文本文件(*.txt)|*.txt|所有文件(*.*)|*.*",
                Multiselect = true
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;
            _errorMsg = @"失败文件名：";
            pbCount.Maximum = ofd.FileNames.Length;
            pbCount.Value = 0;
            lblTotal.Text = ofd.FileNames.Length.ToString(CultureInfo.InvariantCulture);
            foreach (var fileName in ofd.FileNames)
            {
                try
                {
                    string[] file = File.ReadAllLines(fileName);
                    var collapsePillarsName =
                        fileName.Substring(fileName.LastIndexOf(@"\", StringComparison.Ordinal) + 1).Split('.')[0];
                    CollapsePillar collapsePillar = CollapsePillar.FindAllByProperty("name", collapsePillarsName).FirstOrDefault();
                    if (collapsePillar == null)
                    {
                        collapsePillar = new CollapsePillar
                        {
                            xtype = "0",
                            bid = IdGenerator.NewBindingId(),
                            name = collapsePillarsName
                        };
                    }
                    else
                    {
                        collapsePillar.name = collapsePillarsName;
                    }

                    var collapsePillarsPoints = new List<CollapsePillarPoint>();
                    //添加关键点
                    for (int i = 0; i < file.Length - 1; i++)
                    {
                        var collapsePillarsPoint = new CollapsePillarPoint
                        {
                            coordinate_x = Convert.ToDouble(file[i].Split(',')[0]),
                            coordinate_y = Convert.ToDouble(file[i].Split(',')[1]),
                            coordinate_z = 0.0,
                            bid = IdGenerator.NewBindingId(),
                            collapse_pillar = collapsePillar
                        };
                        collapsePillarsPoints.Add(collapsePillarsPoint);
                    }
                    collapsePillar.collapse_pillar_points = collapsePillarsPoints;
                    collapsePillar.Save();
                    ModifyXlz(collapsePillarsPoints, collapsePillar.bid);
                    lblSuccessed.Text = lblSuccessed.Text =
                        (Convert.ToInt32(lblSuccessed.Text) + 1).ToString(CultureInfo.InvariantCulture);
                    pbCount.Value++;
                }
                catch (Exception)
                {
                    lblError.Text =
                      (Convert.ToInt32(lblError.Text) + 1).ToString(CultureInfo.InvariantCulture);
                    lblSuccessed.Text =
                        (Convert.ToInt32(lblSuccessed.Text) - 1).ToString(CultureInfo.InvariantCulture);
                    _errorMsg += fileName.Substring(fileName.LastIndexOf(@"\", StringComparison.Ordinal) + 1) + "\n";
                    btnDetails.Enabled = true;
                }

            }
            Alert.AlertMsg("导入成功！");
        }

        private void btnDetails_Click(object sender, EventArgs e)
        {
            Alert.AlertMsg(_errorMsg);
        }
    }
}