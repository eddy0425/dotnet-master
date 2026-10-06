using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace DotNet.HalconAlgo
{
    public class GenericModelStrategy : ParaStrategyBase<GenericModel>, IRoiEditable, ITemplateEditable
    {
        public override AlgoEnum Algorithm => AlgoEnum.GenericModel;
        public override string Name { get; set; } = "通用匹配";
        public override int RunIndex { get; set; }

        public override void GenTreeNode(ITreeVisualizer tree)
        {
            tree.Branch(Name, branch => branch
                       .Node("坐标系", OutEnum.Coord, line => line
                           .Branch("原点", pt => pt
                               .Node("行", OutEnum.Number)
                               .Node("列", OutEnum.Number)
                           )
                           .Node("角度", OutEnum.Number)
                       )
                   );

            ClearResolvers();
            RegisterOutput("TmplPoint", () => inPara.TmplPoint);
            RegisterOutput("坐标系", () => inPara.Coord);
            RegisterOutput("坐标系/原点", () => inPara.Coord.Center);
            RegisterOutput("坐标系/原点/行", () => inPara.Coord.Y);
            RegisterOutput("坐标系/原点/列", () => inPara.Coord.X);
            RegisterOutput("坐标系/角度", () => inPara.Coord.Angle.Radians);
        }
        public override bool Fun_action(HObject ho_Image, IHDisplay display)
        {
            display.SetImage(ho_Image);
            // 传空集合而不是 null: 另一重载内部会对 strategys 做 ResolveFrom, null 会直接 NRE.
            return Fun_action(display, StrategyExtensions.EmptyList());
        }
        public override bool Fun_action(IHDisplay display, List<IParaStrategy> strategys)
        {
            // 清空必须在所有前置校验之前: Fun_action 的 bool 返回值在每个调用方都被丢弃
            // (各 Form 的 but_Run_Click / but_Cycle_Click 都只调用不判断), 校验失败只是屏幕上多一行红字,
            // 下游工具照旧继续跑。此时若 Results / Coord 还留着上一轮的值, "编辑模板"与下游"坐标系"
            // 读到的就是旧数据, 界面上看不出任何异常。
            ResetOutputs();

            if (inPara.ModelID == null || inPara.ModelID.Length == 0)
            {
                // 与本策略其它文本同一口径: 带 Name 前缀(多工具同屏时才分得清是谁报的),
                // 位置/字号跟随"显示"页的配置。
                display.DispText($"{Name} : 未建立模板，无法执行通用匹配！", new Point2d(inPara.FontX, inPara.FontY), DrawStyle.Of(HColor.Red, inPara.FontSize));
                return false;
            }

            HObject imgReduced; HOperatorSet.GenEmptyObj(out imgReduced);
            HObject ho_SelRect; HOperatorSet.GenEmptyObj(out ho_SelRect);
            HObject ho_Follow; HOperatorSet.GenEmptyObj(out ho_Follow);

            try
            {
                HObject ho_Image = (inPara.ImageIn == "默认")
                    ? display.HoImage.RequireImage(Name)
                    : strategys.ResolveFrom<HObject>(inPara.ImageIn);

                bool useLocalRegion = inPara.RegionIn == "默认";
                HObject ho_Rect;
                if (useLocalRegion)
                {
                    HObject localRegion = inPara.HoRect.HoRegion;

                    // 上游路径的空句柄已由 ResolveRegionFrom 拦下; 本地配置 ROI 不经过它, 需在此补同一层判断,
                    // 否则未绘制 ROI 时 CountObj() 为 0, 下面的循环一次都不进, 静默跑出 0 个结果。
                    if (!localRegion.IsUsableRegion())
                    {
                        display.DispText($"{Name} : 尚未绘制 ROI，无法执行匹配！", new Point2d(inPara.FontX, inPara.FontY), DrawStyle.Of(HColor.Red, inPara.FontSize));
                        return false;
                    }

                    ho_Rect = localRegion;
                }
                else
                {
                    // 上游句柄由 ResolveRegionFrom 保证非空且可用（拿不到就抛），无需再判一次。
                    ho_Rect = strategys.ResolveRegionFrom(inPara.RegionIn);
                }

                // 跟随坐标: 与拟合类同口径, 只把本地配置区域搬到当前工件位姿; 上游区域已在当前图像坐标系。
                // 原先 CoordIn 能在界面上选, 但查找时从未读取 —— 工件一动, 搜索区域仍停在示教位置。
                if (useLocalRegion && inPara.CoordIn != "默认")
                {
                    var inCoord = strategys.ResolveFrom<CvCoord>(inPara.CoordIn);
                    var tmplPoint = strategys.ResolveFrom<Point2d>(inPara.CoordIn.ToTmplPoint());
                    ho_Follow.Dispose();
                    HalconController.TransRegion(new CvCoord(tmplPoint), inCoord, ho_Rect, out ho_Follow);
                    ho_Rect = ho_Follow;
                }

                if (inPara.DispRegion) display.Disp(ho_Rect, DrawStyle.Of(HColor.Blue));

                double bestSoFar = -1;
                int rectCnt = ho_Rect.CountObj();
                for (int j = 0; j < rectCnt; j++)
                {
                    // 释放上一次循环创建的对象，避免 HObject 句柄泄漏
                    imgReduced?.Dispose();
                    ho_SelRect?.Dispose();
                    HOperatorSet.SelectObj(ho_Rect, out ho_SelRect, j + 1);
                    HOperatorSet.ReduceDomain(ho_Image, ho_SelRect, out imgReduced);

                    HOperatorSet.FindGenericShapeModel(imgReduced, inPara.ModelID, out HTuple matchResultID, out HTuple numMatchResult);
                    // 22.11 没有 ClearGenericShapeModelResult, 但结果句柄同样可由 clear_handle 释放;
                    // 取出的轮廓是独立对象, 本轮结果不再引用该句柄。原先一直留给 GC, 连续运行时句柄堆积。
                    try
                    {
                        if (numMatchResult.I <= 0) continue;

                        #region 查找结果
                        HOperatorSet.GetGenericShapeModelResult(matchResultID, "all", "row", out HTuple row);
                        HOperatorSet.GetGenericShapeModelResult(matchResultID, "all", "column", out HTuple column);
                        HOperatorSet.GetGenericShapeModelResult(matchResultID, "all", "angle", out HTuple angle);
                        HOperatorSet.GetGenericShapeModelResult(matchResultID, "all", "score", out HTuple score);
                        #endregion

                        for (int i = 0; i < score.Length; i++)
                        {
                            var result = new ModelResult(row[i], column[i], angle[i], score[i]);
                            inPara.Results.Add(result);

                            // 取该匹配实例对应的轮廓
                            HOperatorSet.GetGenericShapeModelResultObject(out HObject contour, matchResultID, i, "contours");

                            // 只留全局最佳实例的轮廓: 原先每个实例都覆盖一次, 留下的是"最后一个"的轮廓,
                            // 与 Coord / Results[0] 对不上 (编辑模板窗口正是把两者配对显示的)。
                            bool isBest = result.Score > bestSoFar;
                            if (isBest)
                            {
                                bestSoFar = result.Score;
                                inPara.HoContour.Dispose();
                                inPara.HoContour = contour;
                            }

                            try
                            {
                                if (inPara.DispContour) display.Disp(contour, DrawStyle.Of(HColor.Green));
                                if (inPara.DispPoint) display.Disp(result.Coord, DrawStyle.Of(HColor.Red));
                            }
                            finally
                            {
                                if (!isBest) contour.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        HOperatorSet.ClearHandle(matchResultID);
                    }
                }

                // FindGenericShapeModel 只在单个 ROI 内按得分降序; 多 ROI 时 Results[0] 只是第一个 ROI 的最佳。
                // 这里跨 ROI 稳定排序, 让 Results[0] 就是全局最佳: 对外坐标系、"最佳得分"与编辑模板都取它。
                inPara.Results = inPara.Results.OrderByDescending(r => r.Score).ToList();
                if (inPara.Results.Count > 0) inPara.Coord = inPara.Results[0].Coord;

                if (inPara.DispText)
                {
                    int cnt = inPara.Results.Count;
                    double bestScore = cnt > 0 ? inPara.Results[0].Score : 0;
                    string message = $"{Name} : 数量:{cnt} 最佳得分:{bestScore:F3} 缩放:[{inPara.ScaleMin},{inPara.ScaleMax}] 角度范围:[{inPara.AngleStart}°,{(inPara.AngleStart.D + inPara.AngleExtent.D)}°]";
                    // 0 匹配用红字: 绿字在本仓的惯例里是"正常跑完", 一个都没找到不该长得跟成功一样。
                    display.DispText(message, new Point2d(inPara.FontX, inPara.FontY), DrawStyle.Of(cnt > 0 ? HColor.Green : HColor.Red, inPara.FontSize));
                }

                return true;
            }
            catch
            {
                // 循环中途抛异常时, 前面 ROI 的结果已写进 Results / HoContour: 与开头同样复位,
                // 不把半截结果留给"编辑模板"和下游 (宿主吞掉异常后下游照跑)。
                ResetOutputs();
                throw;
            }
            finally
            {
                imgReduced?.Dispose();
                ho_SelRect?.Dispose();
                ho_Follow.Dispose();
            }
        }
        /// <summary>
        /// 复位本轮输出: 结果列表、坐标系, 以及与坐标系配对显示的轮廓
        /// (DispROI / "编辑模板"一起取用, 不清的话失败轮次会把旧轮廓配上零坐标系)。
        /// </summary>
        private void ResetOutputs()
        {
            inPara.Results = new List<ModelResult>();
            inPara.Coord = new CvCoord();
            inPara.HoContour?.Dispose();
            HOperatorSet.GenEmptyObj(out inPara.HoContour);
        }

        public override void DispPara(IParaUiHost ui)
        {
            ui.ShowTabs(TabPageEnum.Parameter, TabPageEnum.Region, TabPageEnum.Matching, TabPageEnum.Display);

            CvRegion hRegion = inPara.HoRect;
            ui.ShowComboBox("cmb_Width", hRegion.Width.ToString(), false);
            ui.ShowComboBox("cmb_Height", hRegion.Height.ToString(), false);
            ui.ShowComboBox("cmb_TopLeft", $"{hRegion.TopLeft.X};{hRegion.TopLeft.Y}", false);
            ui.ShowComboBox("cmb_BottomRight", $"{hRegion.BottomRight.X};{hRegion.BottomRight.Y}", false);
            ui.ShowComboBox("cmb_Center", $"{hRegion.Center.X};{hRegion.Center.Y}", false);

            ui.ShowComboBox("cmb_CoordIn", inPara.CoordIn.ToString(), false);

            //基本参数
            ui.ShowLabel("lbl_100", "图像来源");
            ui.ShowComboBox("cmb_100", inPara.ImageIn.ToString(), false);
            ui.ShowButton("btn_100", true);

            ui.ShowLabel("lbl_101", "区域来源");
            ui.ShowComboBox("cmb_101", inPara.RegionIn, false);
            ui.ShowButton("btn_101", true);

            ui.ShowLabel("lbl_102", "起始角度");
            ui.ShowComboBoxList("cmb_102", inPara.AngleStart.ToString(), new[] { "-90", "-45" });
            ui.ShowButton("btn_102", false);

            ui.ShowLabel("lbl_103", "增量角度");
            ui.ShowComboBoxList("cmb_103", inPara.AngleExtent.ToString(), new[] { "90", "180" });
            ui.ShowButton("btn_103", false);

            ui.ShowLabel("lbl_104", "最大重叠率");
            ui.ShowComboBoxList("cmb_104", inPara.MaxOverlap.ToString(), new[] { "0", "0.3", "0.5" });
            ui.ShowButton("btn_104", false);

            ui.ShowLabel("lbl_110", "匹配数量");
            ui.ShowComboBoxList("cmb_110", inPara.NumMatches == 0 ? "多个" : inPara.NumMatches.ToString(), new[] { "1", "2", "3", "多个" });
            ui.ShowButton("btn_110", false);

            ui.ShowLabel("lbl_111", "得分");
            ui.ShowComboBoxDropDown("cmb_111", inPara.MinScore.ToString(), new[] { "0.5", "0.7" });

            ui.ShowLabel("lbl_112", "金字塔");
            ui.ShowComboBoxList("cmb_112", inPara.NumLevels.ToString(), new[] { "0", "2" });

            ui.ShowLabel("lbl_113", "最小缩放");
            ui.ShowComboBoxDropDown("cmb_113", inPara.ScaleMin.ToString(), new[] { "0.8", "0.7" });

            ui.ShowLabel("lbl_114", "最大缩放");
            ui.ShowComboBoxDropDown("cmb_114", inPara.ScaleMax.ToString(), new[] { "1.2", "1.5" });

            //------------------------------------------
            ui.ShowCheckBox("ckb_disp0", "显示文本", inPara.DispText);
            ui.ShowCheckBox("ckb_disp1", "查找区域", inPara.DispRegion);
            ui.ShowCheckBox("ckb_disp2", "显示轮廓", inPara.DispContour);
            ui.ShowCheckBox("ckb_disp3", "显示点", inPara.DispPoint);

            ui.ShowComboBoxDropDown("CB_FontX", inPara.FontX.ToString(), new[] { "20", "50" });
            ui.ShowComboBoxDropDown("CB_FontY", inPara.FontY.ToString(), new[] { "20", "50" });
            ui.ShowComboBoxDropDown("CB_FontSize", inPara.FontSize.ToString(), new[] { "15", "30" });
        }
        public override void SavePara(IParaUiHost ui)
        {
            //基本参数
            inPara.CoordIn = ui.GetString("cmb_CoordIn");
            inPara.ImageIn = ui.GetString("cmb_100");
            inPara.RegionIn = ui.GetString("cmb_101");
            inPara.AngleStart = ui.GetDouble("cmb_102");
            inPara.AngleExtent = ui.GetDouble("cmb_103");
            inPara.MaxOverlap = ui.GetDouble("cmb_104");

            inPara.NumMatches = ui.GetString("cmb_110") == "多个" ? 0 : ui.GetInt("cmb_110");
            inPara.MinScore = ui.GetDouble("cmb_111");
            inPara.NumLevels = ui.GetInt("cmb_112");
            inPara.ScaleMin = ui.GetDouble("cmb_113");
            inPara.ScaleMax = ui.GetDouble("cmb_114");

            //------------------------------------------
            inPara.DispText = ui.GetBool("ckb_disp0");
            inPara.DispRegion = ui.GetBool("ckb_disp1");
            inPara.DispContour = ui.GetBool("ckb_disp2");
            inPara.DispPoint = ui.GetBool("ckb_disp3");

            inPara.FontX = ui.GetInt("CB_FontX");
            inPara.FontY = ui.GetInt("CB_FontY");
            inPara.FontSize = ui.GetInt("CB_FontSize");

            // 界面值全部读完之后再写模型: HALCON 拒收某个值 (如得分 > 1) 时会抛异常,
            // 原先写模型夹在中间, 一抛"显示"页的改动就全部丢失。
            // 仅当模板已建立时才更新模型参数；否则等下一次 SetTemplateAsync 时统一应用
            if (inPara.ModelID != null && inPara.ModelID.Length > 0)
            {
                // 注意：iso_scale_*, num_levels 等属于"修改模型"参数，改动后必须重新训练。
                // 这里 SavePara 没有图像可重训，所以只更新"查找"类参数；
                // 如需修改 iso_scale / num_levels / optimization，请用户重新设置模板触发 SetTemplateAsync。
                HTuple angleStartRad = ((HTuple)inPara.AngleStart.D).TupleRad();
                HTuple angleEndRad = ((HTuple)(inPara.AngleStart.D + inPara.AngleExtent.D)).TupleRad();

                HOperatorSet.SetGenericShapeModelParam(inPara.ModelID, "angle_start", angleStartRad);
                HOperatorSet.SetGenericShapeModelParam(inPara.ModelID, "angle_end", angleEndRad);
                HOperatorSet.SetGenericShapeModelParam(inPara.ModelID, "max_overlap", inPara.MaxOverlap);
                HOperatorSet.SetGenericShapeModelParam(inPara.ModelID, "num_matches", inPara.NumMatches);
                HOperatorSet.SetGenericShapeModelParam(inPara.ModelID, "min_score", inPara.MinScore);
                HOperatorSet.SetGenericShapeModelParam(inPara.ModelID, "greediness", inPara.Greediness);
                HOperatorSet.SetGenericShapeModelParam(inPara.ModelID, "subpixel", inPara.SubPixel);
            }
        }
        public async Task DrawROIAsync(IRoiHost host, RectEnum type, bool newROI)
        {
            if (newROI)
            {
                // Type 必须在绘制前写入(HDisplay 按它分发图元), 但取消时几何不会被回写,
                // 所以要连 Type 一起还原, 否则 Type 与 HoRegion / 外接框对不上, 还会存进 job 配置.
                var prevType = inPara.HoRect.Type;
                inPara.HoRect.Type = type;
                if (!await host.DrawRegionAsync(inPara.HoRect))
                    inPara.HoRect.Type = prevType;
            }
            else await host.DrawRegionModAsync(inPara.HoRect);

            // 这里故意不短路: 取消后仍要把原 ROI 重画回去(ParaForm 事先 ReDispImage 已清屏)
            host.Display.Disp(inPara.HoRect, DrawStyle.Of(HColor.Blue));
            host.SetRectPara(inPara.HoRect);
        }
        public void DispROI(IRoiHost host)
        {
            host.SetModelPara(inPara.HoRect.HoRegion, inPara.HoContour, inPara.Coord);
        }
        public async Task SetTemplateAsync(IRoiHost host, RectEnum type, bool newModel)
        {
            HObject imgReduced; HOperatorSet.GenEmptyObj(out imgReduced);
            HTuple matchResultID = null;
            HObject newContour = null;
            HTuple modelID = null;
            // 绘制会就地改写 ModeRect (Type / 几何 / 区域)。走不到"提交"这一步 —— 取消、试匹配失败、
            // 取图 / 训练 / 存图抛异常 —— 都整体换回快照, 模板区域始终与仍在用的模板配套。
            CvRegion snapshot = inPara.ModeRect.Clone();

            try
            {
                // Type 必须在绘制前写入(DrawRegionAsync 按它分发图元)
                inPara.ModeRect.Type = type;

                bool confirmed = newModel
                    ? await host.DrawRegionAsync(inPara.ModeRect)
                    : await host.DrawRegionModAsync(inPara.ModeRect);

                // 取消 / 超时: 必须直接返回.
                // 继续往下会按"旧几何 + 新参数"重建一份用户没有要求的模板,
                // 原模板就此丢失 —— 这正是"取消不应有副作用"的关键一步.
                if (!confirmed)
                {
                    RestoreModeRect(ref snapshot);
                    // 绘制会话结束时窗口只剩底图, 把原模板区域重新画回去, 避免画面像是被清空
                    host.Display.Disp(inPara.ModeRect, DrawStyle.Of(HColor.Orange));
                    return;
                }

                var hImage = host.Display.HoImage.RequireImage(Name);

                imgReduced.Dispose();
                HOperatorSet.ReduceDomain(hImage, inPara.ModeRect.HoRegion, out imgReduced);

                #region 1) 创建模板
                HOperatorSet.CreateGenericShapeModel(out modelID);
                #endregion

                #region 2) 训练前必须设置的“修改模型”类参数 (改动这些参数需要重训)
                HOperatorSet.SetGenericShapeModelParam(modelID, "num_levels", inPara.NumLevels);
                HOperatorSet.SetGenericShapeModelParam(modelID, "iso_scale_min", inPara.ScaleMin);
                HOperatorSet.SetGenericShapeModelParam(modelID, "iso_scale_max", inPara.ScaleMax);
                HOperatorSet.SetGenericShapeModelParam(modelID, "optimization", "auto");
                HOperatorSet.SetGenericShapeModelParam(modelID, "metric", "use_polarity");
                #endregion

                #region 3) 训练
                HOperatorSet.TrainGenericShapeModel(imgReduced, modelID);
                #endregion

                #region 4) 训练后设置的“查找”类参数 (角度需为弧度)
                HTuple angleStartRad = ((HTuple)inPara.AngleStart.D).TupleRad();
                HTuple angleEndRad = ((HTuple)(inPara.AngleStart.D + inPara.AngleExtent.D)).TupleRad();

                HOperatorSet.SetGenericShapeModelParam(modelID, "angle_start", angleStartRad);
                HOperatorSet.SetGenericShapeModelParam(modelID, "angle_end", angleEndRad);
                HOperatorSet.SetGenericShapeModelParam(modelID, "max_overlap", inPara.MaxOverlap);
                HOperatorSet.SetGenericShapeModelParam(modelID, "min_score", inPara.MinScore);
                HOperatorSet.SetGenericShapeModelParam(modelID, "greediness", inPara.Greediness);
                HOperatorSet.SetGenericShapeModelParam(modelID, "subpixel", inPara.SubPixel);

                // 试模板时只需要 1 个匹配
                HOperatorSet.SetGenericShapeModelParam(modelID, "num_matches", 1);
                #endregion

                #region 5) 试匹配
                // 先用新模板试匹配, 确认可用后再替换: 若先替换, 试匹配失败时旧模板已被释放,
                // 新模板却配着旧模板示教出的 TmplPoint, 下游跟随会静默偏移。
                // 与另外三种匹配同口径只搜模板区域 (原先搜整图): 图里有同样的工件时, 同分的另一个可能排在前面,
                // 示教原点就落到了别的工件上。
                HOperatorSet.FindGenericShapeModel(imgReduced, modelID, out matchResultID, out HTuple numMatchResult);
                #endregion

                // 试匹配完成后，恢复用户设定的匹配数量供 Fun_action 使用
                HOperatorSet.SetGenericShapeModelParam(modelID, "num_matches", inPara.NumMatches);

                // 一个都没找到时必须在取结果之前返回: 对 0 个结果取索引 0 会直接抛 HALCON 错误。
                // 新模板在 finally 里丢弃, 旧模板与旧 TmplPoint 仍是一致的一对。
                if (numMatchResult.I <= 0)
                {
                    RestoreModeRect(ref snapshot);
                    host.Display.Disp(inPara.ModeRect, DrawStyle.Of(HColor.Orange));
                    host.Display.DispText("新建模板失败！", new Point2d(10, 10), DrawStyle.Of(HColor.Red));
                    return;
                }

                HOperatorSet.GetGenericShapeModelResult(matchResultID, 0, "row", out HTuple row);
                HOperatorSet.GetGenericShapeModelResult(matchResultID, 0, "column", out HTuple column);
                HOperatorSet.GetGenericShapeModelResult(matchResultID, 0, "angle", out HTuple angle);
                HOperatorSet.GetGenericShapeModelResult(matchResultID, 0, "score", out HTuple score);

                var result = new ModelResult(row, column, angle, score);
                result.ResultID = matchResultID;
                HOperatorSet.GetGenericShapeModelResultObject(out newContour, matchResultID, 0, "contours");

                // 模板图先落盘再提交: 原先先换模板再存图, 存图一失败, 新模板就配着旧 TmplPoint / 旧 ModelPath,
                // 下游跟随静默偏移。现在保存失败时新模板在 finally 里丢弃, 旧的一套 (含旧模板图) 原样保留。
                string modelPath = Path.Combine(AlgoPaths.JobDir, RunIndex.ToString(), "matching.bmp");
                ModelImage.Save(hImage, imgReduced, modelPath);

                // 提交: 以下只做赋值与释放旧句柄, 模型 / 路径 / 结果 / 示教原点一起换。
                if (inPara.ModelID != null && inPara.ModelID.Length > 0)
                {
                    HOperatorSet.ClearHandle(inPara.ModelID);
                }
                inPara.ModelID = modelID;
                modelID = null;
                inPara.ModelPath = modelPath;
                inPara.Results = new List<ModelResult> { result };
                inPara.HoContour.Dispose();
                inPara.HoContour = newContour;
                newContour = null;
                inPara.Coord = result.Coord;
                inPara.TmplPoint = new Point2d(result.X, result.Y);      //更改跟随坐标
                // 试匹配结果句柄随 result.ResultID 交给宿主 (编辑模板窗口据此取轮廓), 不在这里释放
                matchResultID = null;
                snapshot.Dispose();
                snapshot = null;

                host.SetModelPara(inPara.HoRect.HoRegion, inPara.HoContour, inPara.Coord);
                host.Display.Disp(inPara.ModeRect, DrawStyle.Of(HColor.Orange));

                host.DrawDone(inPara.ModelPath, inPara.ModeRect.HoRegion, inPara.HoContour, result);

                host.Display.DispText("新建模板成功！", new Point2d(10, 10), DrawStyle.Of(HColor.Green));
            }
            finally
            {
                // 未提交: 丢弃新模板; 模板区域换回快照 (取消 / 试匹配失败已提前换回, 这里兜底异常路径)
                if (modelID != null) HOperatorSet.ClearHandle(modelID);
                if (matchResultID != null) HOperatorSet.ClearHandle(matchResultID);
                if (snapshot != null) RestoreModeRect(ref snapshot);
                newContour?.Dispose();
                imgReduced.Dispose();
            }
        }

        /// <summary>丢弃绘制改写过的 ModeRect, 换回绘制前的快照。</summary>
        private void RestoreModeRect(ref CvRegion snapshot)
        {
            inPara.ModeRect.Dispose();
            inPara.ModeRect = snapshot;
            snapshot = null;
        }

    }

    public class GenericModel : AlgoFont
    {
        public GenericModel() 
        {
            HOperatorSet.GenEmptyObj(out HoContour);
        }

        /// <summary> 图像来源 </summary>
        public string ImageIn { set; get; } = "默认";

        /// <summary> 区域来源 </summary>
        public string RegionIn { set; get; } = "默认";

        /// <summary> 跟随坐标 </summary>
        public string CoordIn { set; get; } = "默认";

        /// <summary> 坐标系 </summary>
        public CvCoord Coord { get; set; } = new CvCoord();

        /// <summary> 模版坐标 </summary>
        public Point2d TmplPoint { get; set; } = new Point2d();

        /// <summary> 区域 </summary>
        public CvRegion HoRect { set; get; } = new CvRegion();

        /// <summary> 模版区域 </summary>
        public CvRegion ModeRect { set; get; } = new CvRegion();

        /// <summary> 模版轮廓 </summary>
        /// <remarks>不加 = new HObject() 初始化器：句柄统一由构造函数的 GenEmptyObj 创建，否则初始化器创建的句柄会被覆盖且永不释放。</remarks>
        [JsonIgnore]
        public HObject HoContour;

        /// <summary> 模版路径 </summary>
        /// <remarks>尚未创建模板时为空串而不是 null：这个值会直接交给 ReadImage / DisplayModel，空串换来一条
        /// 「文件打不开」的明确报错，null 换来的是调用点的 NRE。</remarks>
        public string ModelPath { get; set; } = string.Empty;

        /// <summary> 模板ID </summary>
        /// <remarks>可空：模板尚未创建、或参数变更后被主动置 null（见 CreateModel），调用点一律先判
        /// <c>ModelID == null || ModelID.Length == 0</c> 再用。</remarks>
        [JsonIgnore]
        public HTuple ModelID { get; set; }

        /// <summary> 起始角度 </summary>
        public HTuple AngleStart { get; set; } = -90;

        /// <summary> 增量角度 </summary>
        public HTuple AngleExtent { get; set; } = 180;

        /// <summary> 得分 </summary>
        public HTuple MinScore { get; set; } = 0.6;

        /// <summary> 匹配数量 </summary>
        public HTuple NumMatches { get; set; } = 1;

        /// <summary> 最大重叠率 </summary>
        public HTuple MaxOverlap { get; set; } = 0.5;

        /// <summary>
        /// 子像素： 如果不等于“none”，则为亚像素精度。默认值：“最小二乘” true
        /// </summary>
        public HTuple SubPixel { get; set; } = "least_squares";

        /// <summary> 金字塔层数 </summary>
        public HTuple NumLevels { get; set; } = 0;

        /// <summary>
        /// 贪婪系数 (形状匹配、缩放匹配)  
        /// 搜索启发式的“贪婪”（0：安全但缓慢；1：快速但匹配可能错过）。默认值：0.9
        /// </summary>
        public HTuple Greediness { get; set; } = 0.7;

        /// <summary> 最小缩放 (缩放匹配) </summary>
        public HTuple ScaleMin { get; set; } = 0.8;

        /// <summary> 最大缩放 (缩放匹配) </summary>
        public HTuple ScaleMax { get; set; } = 1.2;

        /// <summary> 匹配结果 </summary>
        /// <remarks>初始化为空列表：Run 每轮开头都会整体替换它，但「从未跑过」的参数对象也得能被 UI 安全读取。</remarks>
        public List<ModelResult> Results { get; set; } = new List<ModelResult>();

        /// <summary> 显示区域 </summary>
        public bool DispRegion { set; get; } = true;

        /// <summary> 显示轮廓 </summary>
        public bool DispContour { set; get; } = true;

        /// <summary> 显示点 </summary>
        public bool DispPoint { set; get; } = true;

    }
}
