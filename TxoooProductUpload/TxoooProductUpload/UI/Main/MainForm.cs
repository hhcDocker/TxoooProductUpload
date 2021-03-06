﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TxoooProductUpload.UI.Main
{
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using TxoooProductUpload.Service;
    using TxoooProductUpload.Service.Entities;
    using TxoooProductUpload.Common;
    using Newtonsoft.Json;
    using TxoooProductUpload.UI.ImageDownload;
    using Microsoft.Win32;
    using FSLib.Network.Http;
    using System.Threading;

    partial class MainForm : FormBase
    {
        #region 页面变量
        //h5.m.taobao.com/awp/core/detail.htm|item.taobao.com|
        Regex detailUrlReg = new Regex("detail.tmall.com|detail.m.tmall.com|detail.1688.com|item.jd.com|item.m.jd.com|m.1688.com");//url
        Regex storeUrlReg = new Regex("https://[\\S\\s]+.tmall.com");
        long _classId = 0;
        long _regionCode = 0;
        string _regionName = string.Empty;
        int _new_old = 1;
        bool _is_virtual = false;
        bool _product_ispostage = true;
        int _refund = 1;
        int _postage = 0;
        int _append = 0;
        int _limit = 0;
        int _typeService = 1;
        int _radio_num = 0;

        Queue<ProductResult> WaitProcessProducts = new Queue<ProductResult>();   //等待上传商品
        Dictionary<string, ProductResult> FailProducts = new Dictionary<string, ProductResult>();  //上传失败的商品
        #endregion

        public MainForm()
        {
            InitializeComponent();
            Load += MainForm_Load;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await InitServiceContext();
            this.Text = ConstParams.APP_NAME + string.Format(" V{0}    PowerBy:{1}"
                , ConstParams.Version.Substring(0, ConstParams.Version.LastIndexOf('.'))
                , ConstParams.APP_AUTHOR);
            InitToolbar();
            InitStatusBar();
            InitFormControl();
        }

        #region 状态栏和工具栏事件
        /// <summary>
        /// 页面功能控件事件
        /// </summary>
        void InitFormControl()
        {

            btnOneKeyOk.Click += async (s, e) => await ProcessProduct();
            txtOneKeyUrl.Click += (s, e) => ClipboardToTextBox();

            //分类CombBox级联事件
            tsClass1.SelectedIndexChanged += (s, e) => cbClass_SelectedIndexChanged(s, e);
            tsClass2.SelectedIndexChanged += (s, e) => cbClass_SelectedIndexChanged(s, e);
            tsClass3.SelectedIndexChanged += (s, e) => cbClass_SelectedIndexChanged(s, e);
            tsClass4.SelectedIndexChanged += (s, e) => cbClass_SelectedIndexChanged(s, e);

            //发货地CombBox级联事件
            cbArea1.SelectedIndexChanged += (s, e) => cbArea_SelectedIndexChanged(s, e);
            cbArea2.SelectedIndexChanged += (s, e) => cbArea_SelectedIndexChanged(s, e);

            //RadioButton Change事件 
            rbTypeNew.CheckedChanged += (s, e) => rb_CheckedChanged(s, e);
            rbVirtualTrue.CheckedChanged += (s, e) => rb_CheckedChanged(s, e);
            rbPostageTrue.CheckedChanged += (s, e) => rb_CheckedChanged(s, e);
            rbRefundTrue.CheckedChanged += (s, e) => rb_CheckedChanged(s, e);

            //NumericUpDown Change事件    
            tbPostage.ValueChanged += (s, e) => tb_CheckedChanged(s, e);
            tbappend.ValueChanged += (s, e) => tb_CheckedChanged(s, e);
            tbLinit.ValueChanged += (s, e) => tb_CheckedChanged(s, e);

        }
        /// <summary>
        /// 初始化工具栏
        /// </summary>
        void InitToolbar()
        {
            tsExit.Click += (s, e) => Close();
            tsLogin.Enabled = !(tsImgManage.Enabled =
                tsDataPack.Enabled = gbSetting.Enabled = gbSearch.Enabled = gbBase.Enabled = tsComment.Enabled =
               tsLogout.Enabled = _context.Session.IsLogined);
            tsComment.Click += (s, e) => { new Comment(_context).ShowDialog(this); };
            tsImgManage.Click += (s, e) => { new Crawler(_context).Show(this); };
            tsLogin.Click += Login;
            tsLogout.Click += Logout;
            this.txtOneKeyUrl.SetHintText("请输入天猫、淘宝、京东、阿里巴巴等商品链接");
            //捕捉登录状态变化事件，在登录状态发生变化的时候重设登录状态
            _context.Session.IsLoginedChanged += async (s, e) => await LoginedChanged();
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Login(object sender, EventArgs e)
        {
            AppendLog(txtLog, "登录中...");
            if (new Login(_context).ShowDialog(this) != DialogResult.OK)
            {
                AppendLog(txtLog, "登录取消...");
            }
        }
        /// <summary>
        /// 注销
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Logout(object sender, EventArgs e)
        {
            //清空数据
            //foreach (Control ctl in gbArea.Controls)
            //{
            //    if (ctl is ComboBox)
            //    {
            //        var combobox = ctl as ComboBox;
            //        if (combobox.DataSource != null)
            //        {
            //            combobox.DataSource = null;
            //        }
            //    }
            //}
            //foreach (Control ctl in gbClass.Controls)
            //{
            //    if (ctl is ComboBox)
            //    {
            //        var combobox = ctl as ComboBox;
            //        if (combobox.DataSource != null)
            //        {
            //            combobox.DataSource = null;
            //        }
            //    }
            //}
            //txtLog.Text = string.Empty;
            _context.CacheContext.Save();
            _context.Session.Logout();
            AppendLogWarning(txtLog, "退出登录成功！");
        }


        /// <summary>
        /// 登录状态变化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        async Task LoginedChanged()
        {
            tsLogin.Enabled = !(tsImgManage.Enabled =
             tsDataPack.Enabled = gbSetting.Enabled = gbSearch.Enabled = gbBase.Enabled = tsComment.Enabled =
            tsLogout.Enabled = _context.Session.IsLogined);
            tsLogin.Text = _context.Session.IsLogined ? string.Format("已登录为【{0} ({1})】", _context.Session.LoginInfo.DisplayName, _context.Session.LoginInfo.UserName) : "登录(&I)";
            if (_context.Session.IsLogined)
            {
                AppendLog(txtLog, "登录成功...");
                AppendLog(txtLog, tsLogin.Text);
                try
                {
                    BeginOperation("开始更新缓存数据...");
                    await _context.CacheContext.Update(_context);
                    //绑定combobox
                    tsClass1.DataSource = _context.ClassDataService.RootClassList;
                    cbArea1.DataSource = _context.CacheContext.Data.AreaList.Where(m => m.parent_id == 1).ToList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    EndOperation("缓存更新完成...");
                }
            }
        }
        /// <summary>
        /// 初始化状态栏
        /// </summary>
        void InitStatusBar()
        {
            //绑定链接处理
            foreach (var label in st.Items.OfType<ToolStripStatusLabel>().Where(s => s.IsLink && s.Tag != null))
            {
                label.Click += (s, e) =>
                {
                    try
                    {
                        Process.Start((s as ToolStripStatusLabel).Tag.ToString());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "错误：无法打开网址，错误信息：" + ex.Message + "。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    }
                };
            }
        }

        /// <summary>
        /// 表示开始一个操作
        /// </summary>
        /// <param name="opName">当前操作的名称</param>
        /// <param name="maxItemsCount">当前操作如果需要显示进度，那么提供任务总数；不提供则为跑马灯等待</param>
        /// <param name="disableForm">是否禁用当前窗口的操作</param>
        void BeginOperation(string opName, int maxItemsCount = 100, bool disableForm = false)
        {
            stStatus.Text = opName.DefaultForEmpty("正在操作，请稍等...");
            AppendLog(txtLog, stStatus.Text);
            stProgress.Visible = true;
            stProgress.Maximum = maxItemsCount > 0 ? maxItemsCount : 100;
            stProgress.Style = maxItemsCount > 0 ? ProgressBarStyle.Blocks : ProgressBarStyle.Marquee;
            if (disableForm)
            {
                btnOneKeyOk.Enabled = false;
            }
        }

        /// <summary>
        /// 操作结束
        /// </summary>
        void EndOperation(string opName = "就绪.")
        {
            AppendLog(txtLog, opName);
            stStatus.Text = opName;
            stProgress.Visible = false;
            btnOneKeyOk.Enabled = true;
        }
        #endregion

        #region 服务接入

        /// <summary>
        /// 初始化服务状态
        /// </summary>
        async Task InitServiceContext()
        {
            _context = new ServiceContext();
            BeginOperation("正在初始化配置信息...", 0, true);
            await Task.Delay(1000);
            EndOperation();
        }

        #endregion

        #region 一键上传
        async Task ProcessProduct()
        {
            string url = txtOneKeyUrl.Text.Trim();

            if (string.IsNullOrEmpty(url))
            {
                IS("哎呀，没有商品链接，逗我呢 o(╯□╰)o");
                txtOneKeyUrl.Focus();
                return;
            }

            if (!CheckClass() || !CheckArea() || !CheckBaseInfo()) { return; }

            BeginOperation("开始抓取...", 0, true);
            if (detailUrlReg.IsMatch(url))
            {
                try
                {
                    ProductResult _result = await _context.UrlConvertProductService.ProcessProduct(url);
                    await ProcessProductAndUpload(_result);  // 上传
                }
                catch (Exception ex)
                {
                    AppendLogError(txtLog, ex.Message);
                }
            }
            else if (storeUrlReg.IsMatch(url))
            {
                await ProcessStoreUrl(url);
            }
            else
            {
                IS("暂时店铺链接只支持天猫整店抓取\n商品链接只支持天猫，阿里巴巴，和京东！\n如有其他需求，请联系作者!");
                EndOperation("不支持的连接");
            }
            EndOperation();
        }

        #endregion

        #region 单击文本框将剪切板内容复制到文本框
        /// <summary>
        /// 单击文本框将剪切板内容复制到文本框
        /// </summary>
        void ClipboardToTextBox()
        {
            if (string.IsNullOrEmpty(txtOneKeyUrl.Text))
            {
                string getTxt = Clipboard.GetText();
                if (detailUrlReg.IsMatch(getTxt) || storeUrlReg.IsMatch(getTxt))
                {
                    txtOneKeyUrl.Text = getTxt;
                }
            }
            else
            {
                txtOneKeyUrl.SelectAll();
            }
        }

        #endregion

        #region 分类相关
        /// <summary>
        /// 分类ComboBoxChanged事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        void cbClass_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox currentTsCombbox = sender as ComboBox;
            var selectDate = currentTsCombbox.SelectedItem as ProductClassInfo;

            if (currentTsCombbox.Name == "tsClass1")
            {
                _typeService = selectDate.ClassId == 1 ? 1 : 2;
                tsClass2.DataSource = _context.CacheContext.Data.ProductClassList.Where(m => m.ParentId == selectDate.ClassId).ToList();
            }
            else if (currentTsCombbox.Name == "tsClass2")
            {
                tsClass3.DataSource = _context.CacheContext.Data.ProductClassList.Where(m => m.ParentId == selectDate.ClassId).ToList();
            }
            else if (currentTsCombbox.Name == "tsClass3")
            {
                tsClass4.DataSource = selectDate.RadioNums;
                _classId = selectDate.ClassId;
                return;
            }
            else if (currentTsCombbox.Name == "tsClass4")
            {
                stStatus.Text = tsClass5.Text = string.Format("分类：{0} | 比例：{1}", (tsClass3.SelectedItem as ProductClassInfo).ClassName
                    , currentTsCombbox.SelectedItem.ToString());
                txtPrice.Value = Convert.ToDecimal(currentTsCombbox.SelectedItem);
            }
        }

        /// <summary>
        /// 验证分类是否选择
        /// </summary>
        /// <returns></returns>
        bool CheckClass()
        {
            if (_classId == 0)
            {
                MessageBox.Show("还没有选择分类呐^_^", "创业赚钱");
                tsClass1.Focus();
                return false;
            }
            return true;
        }
        #endregion

        #region 发货地相关
        /// <summary>
        /// 分类ComboBoxChanged事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        void cbArea_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox currentTsCombbox = sender as ComboBox;
            var selectDate = currentTsCombbox.SelectedItem as AreaInfo;

            if (currentTsCombbox.Name == "cbArea1")
            {
                //过滤直辖市
                if (new int[] { 110000, 120000, 310000, 500000 }.Contains(selectDate.region_code))
                {
                    stStatus.Text = lbArea.Text = "当前选择发货地：" + selectDate.region_name;
                    _regionName = selectDate.region_name;
                    _regionCode = selectDate.region_code;
                    cbArea2.Text = "";
                    cbArea2.Enabled = false;
                    return;
                }
                cbArea2.DataSource = _context.CacheContext.Data.AreaList.Where(m => m.parent_id == selectDate.region_id).ToList();
                cbArea2.Enabled = true;
            }
            else if (currentTsCombbox.Name == "cbArea2")
            {
                stStatus.Text = lbArea.Text = "当前选择发货地：" + selectDate.region_name;
                _regionName = selectDate.region_name;
                _regionCode = selectDate.region_code;
            }
        }

        /// <summary>
        /// 验证分类是否选择
        /// </summary>
        /// <returns></returns>
        bool CheckArea()
        {
            if (_regionCode == 0)
            {
                SM("还没有选择发货地哟^_^");
                cbArea1.Focus();
                return false;
            }
            return true;
        }
        #endregion

        #region 验证
        /// <summary>
        /// RadioButton Change事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void rb_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton currentButton = sender as RadioButton;
            switch (currentButton.Name)
            {
                case "rbTypeNew":  //发布类型为新品
                    _new_old = Convert.ToInt32(rbTypeNew.Checked);
                    AppendLog(txtLog, "发布类型变更为：" + (_new_old == 1 ? "新品" : "二手"));
                    break;
                case "rbVirtualTrue":  //是虚拟商品
                    _is_virtual = rbVirtualTrue.Checked;
                    AppendLog(txtLog, "是否虚拟商品变更为：" + (_is_virtual ? "是" : "不是"));

                    if (_is_virtual)//如果是虚拟商品  包邮 和 退货 不可设置
                    {
                        tbPostage.Value = tbappend.Value = tbLinit.Value = _postage = _append = _limit = 0;
                        rbPostageTrue.Checked = rbRefundTrue.Checked = false;
                        rbPostageFalse.Checked = rbRefundFalse.Checked = true;
                    }
                    gbIspostage.Enabled = gbRefund.Enabled = gbPostage.Enabled = !_is_virtual;

                    break;
                case "rbPostageTrue":  //包邮
                    _product_ispostage = rbPostageTrue.Checked;
                    AppendLog(txtLog, "是否包邮变更为：" + (_product_ispostage ? "包邮" : "不包邮"));
                    if (_product_ispostage)//包邮则清空包邮设置
                    {
                        tbPostage.Value = tbappend.Value = tbLinit.Value = _postage = _append = _limit = 0;
                    }
                    gbPostage.Enabled = !_product_ispostage;
                    break;
                case "rbRefundTrue":  //支持7天无理由退货
                    _refund = Convert.ToInt32(rbRefundTrue.Checked);
                    AppendLog(txtLog, "是否支持7天无理由退货变更为：" + (_refund == 1 ? "支持" : "不支持"));
                    break;
            }

        }
        /// <summary>
        /// 邮费  Change事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void tb_CheckedChanged(object sender, EventArgs e)
        {
            NumericUpDown currentButton = sender as NumericUpDown;
            switch (currentButton.Name)
            {
                case "tbPostage":  //邮费
                    _postage = Convert.ToInt32(tbPostage.Value);
                    stStatus.Text = "当前邮费金额：" + _postage;
                    break;
                case "tbappend":  //递增邮费
                    _append = Convert.ToInt32(tbappend.Value);
                    stStatus.Text = "当前每增加一件邮费金额：" + _append;

                    break;
                case "tbLinit":  //包邮件数
                    _limit = Convert.ToInt32(tbLinit.Value);
                    stStatus.Text = "当前满足包邮件数：" + _limit;
                    break;
            }

        }
        /// <summary>
        /// 验证基本信息
        /// </summary>
        /// <returns></returns>
        bool CheckBaseInfo()
        {
            if (!_is_virtual && !_product_ispostage && _postage == 0)
            {
                SM("还没有设置邮费哟^_^");
                tbPostage.Focus();
                return false;
            }
            return true;
        }
        #endregion

        #region 抓取商品
        /*
         * 1.判断店铺  分页 抓取商品信息 一个线程 
         * 2.处理商品信息 上传任务 一个线程
         * 
         */

        /// <summary>
        /// 根据传入的地址获取所有商品id 以及连接
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        async Task<List<ProductResult>> GetAllProductsByUrl(string url)
        {
            List<ProductResult> list = new List<ProductResult>();
            //https://weishuo.jd.com/view_search-430422-0-5-1-24-1.html
            //https://infshop.1688.com/page/offerlist.htm?showType=windows&sortType=tradenumdown&pageNum=1
            //https://hongdoufushi.tmall.com/category.htm?orderType=hotsell_desc&pageNo=1
            Regex tmReg = new Regex(@"https://([\s\S]*).tmall.com");
            Regex tmDataPageReg = new Regex("(?<= id=\"J_ShopAsynSearchURL\" type=\"hidden\" value=\").+(?=\")");
            Regex tmPageCount = new Regex(@"<b class=\\\""ui-page-s-len\\\"">\d+/(\d+)</b>");
            string tmStartPage = "{0}/category.htm?orderType=hotsell_desc&pageNo={1}";
            string tmDataPageUrl = "";
            string tmDetailUrl = "https://detail.m.tmall.com/item.htm?id={0}";

            Regex aliReg = new Regex(@"https://([\S\s]*).1688.com");

            if (tmReg.IsMatch(url))
            {
                int pageIndex = 1;
                int PageCount = 1;
                var host = tmReg.Match(url).Groups[0].Value;

                do
                {
                    url = tmStartPage.FormatWith(host, pageIndex);
                    var ctx = _context.Session.NetClient.Create<string>(HttpMethod.Get, url, allowAutoRedirect: true);
                    await ctx.SendAsync();
                    if (!ctx.IsValid())
                    {
                        throw new Exception("请求{0}-{1}未能提交".FormatWith(url, "get"));
                    }
                    var html = ctx.Result;

                    tmDataPageUrl = host + tmDataPageReg.Match(html).Value.Replace("&amp;", "&");

                    var ctxData = _context.Session.NetClient.Create<string>(HttpMethod.Get, tmDataPageUrl, allowAutoRedirect: true);
                    await ctxData.SendAsync();
                    if (!ctx.IsValid())
                    {
                        throw new Exception("请求{0}-{1}未能提交".FormatWith(tmDataPageUrl, "get"));
                    }
                    html = ctxData.Result;
                    if (PageCount == 1)
                    {
                        PageCount = Convert.ToInt32(tmPageCount.Match(html).Groups[1].Value);
                    }
                    //下载成功，获得列表
                    var matches = Regex.Matches(html, @"data-id=\\\""(\d+)\\\""", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    //新的任务
                    var newProducts = matches.Cast<Match>()
                                        .Select(s => new ProductResult() { SourceUrl = tmDetailUrl.FormatWith(s.Groups[1].Value) })
                                        .Where(s => !list.Contains(s)).Distinct().ToList();
                    list.AddRange(newProducts);
                } while (++pageIndex < PageCount);
            }

            return list;
        }

        /// <summary>
        /// 处理商品信息并上传
        /// </summary>
        /// <param name="product"></param>
        /// <returns></returns>
        async Task ProcessProductAndUpload(ProductResult product)
        {
            int index = 0;

            if (product.ThumImg.Count == 0) { throw new Exception("未抓取到主图"); }
            if (product.DetailImg.Count == 0) { throw new Exception("未抓取到详情图片"); }

            #region 处理主图
            AppendLog(txtLog, "开始处理主图...");
            try
            {
                await Task.Delay(500);
                if (product.ThumImg == null || product.ThumImg.Count == 0)
                {
                    product.product_imgs = string.Empty;
                }
                else
                {
                    List<string> imgList = new List<string>();
                    index = 1;
                    //排除sku主图
                    var thumImg = product.ThumImg;
                    //if (product.Source == "阿里巴巴" && product.SKU1688 != null)
                    //{
                    //    var skuImgs = product.SKU1688.Where(m => m.prop == "颜色").FirstOrDefault().value.Where(m => !m.image.IsNullOrEmpty()).Select(m => m.image).ToList();
                    //    thumImg = product.ThumImg.Where(m => !skuImgs.Contains(m)).ToList();
                    //}
                    if (thumImg.Count > 5)
                    {
                        thumImg = thumImg.Take(5).ToList();
                    }
                    foreach (var item in thumImg)
                    {
                        var txImgUrl = await _context.ImageService.UploadImg(item, 1);
                        imgList.Add(txImgUrl);
                        AppendLog(txtLog, "第[{0}]张主图上传完成，返回结果{1}...", index++, txImgUrl);
                    }
                    product.product_imgs = imgList.Join(",");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("上传主图出错，错误信息：{0}", ex.Message));
            }
            AppendLog(txtLog, "主图处理结束...");
            #endregion

            #region 生成SKU
            AppendLog(txtLog, "开始处理SKU...");
            try
            {
                string name = "默认规格";
                //处理本地价格
                if (txtPrice.Value > 0)
                {
                    product.ProductPrice = string.Format("{0:F}", Convert.ToDouble(product.ProductPrice) * (1 + Convert.ToDouble(txtPrice.Value.ToString()) / 100));
                }
                await Task.Delay(500);
                //0-编号 1-sku名称（颜色+尺码） 2-价格 3-图片 4-是否默认（id=0为默认）
                string propertyFormat = "&map_id_{0}=0&json_info_{0}={1}&price_{0}={2}&market_price_{0}={2}&remain_inventory_{0}=100&property_map_img_{0}={3}&radio_num_{0}={4}&is_default_{0}={5}";
                index = 0;
                switch (product.Source)
                {
                    case "阿里巴巴":
                        {
                            if (product.SKU1688 != null && product.SKU1688.Count > 0)
                            {
                                var colorList = product.SKU1688.Where(m => m.prop == "颜色").FirstOrDefault();

                                foreach (var colorItem in colorList.value)
                                {
                                    string colorImg = string.Empty;
                                    if (colorItem.image.IsNullOrEmpty())
                                    {
                                        colorImg = await _context.ImageService.UploadImg(product.ThumImg.LastOrDefault(), 3);
                                    }
                                    else
                                    {
                                        colorImg = await _context.ImageService.UploadImg(colorItem.image, 3);
                                    }
                                    var sizeList = product.SKU1688.Where(m => m.prop == "尺码").FirstOrDefault();
                                    if (sizeList != null)
                                    {
                                        foreach (var sizeItem in sizeList.value)
                                        {
                                            name = string.Format("颜色:{0} | 尺码:{1}", colorItem.name, sizeItem.name);
                                            product.product_property += string.Format(propertyFormat, index++, name, product.ProductPrice, colorImg, _radio_num, (index == 1).ToString().ToLower());
                                            AppendLog(txtLog, "第[{0}]个SKU=[{1}]处理完成...", index + 1, name);
                                        }
                                    }
                                    else
                                    {
                                        product.product_property += string.Format(propertyFormat, index++, colorItem.name, product.ProductPrice, colorImg, _radio_num, (index == 1).ToString().ToLower());
                                        AppendLog(txtLog, "第[{0}]个SKU=[{1}]处理完成...", index + 1, name);
                                    }
                                }
                            }
                            else
                            {
                                var colorImg = await _context.ImageService.UploadImg(product.ThumImg.LastOrDefault(), 3);
                                product.product_property += string.Format(propertyFormat, index++, name, product.ProductPrice, colorImg, _radio_num, "true");
                                AppendLog(txtLog, "第[{0}]个SKU=[{1}]处理完成...", index + 1, name);
                            }
                        }
                        break;
                    case "京东":
                        {
                            var colorList = product.SKUJD.colorSize;
                            foreach (var colorItem in colorList)
                            {
                                string colorImg = string.Empty;
                                if (colorItem.image.IsNullOrEmpty())
                                {
                                    colorImg = await _context.ImageService.UploadImg(product.ThumImg.LastOrDefault(), 3);
                                }
                                else
                                {
                                    colorImg = await _context.ImageService.UploadImg(colorItem.image, 3);
                                }
                                if (product.SKUJD.colorSizeTitle.sizeName != null && product.SKUJD.colorSizeTitle.colorName != null)
                                {
                                    name = string.Format("{0}:{1} | {2}:{3}", product.SKUJD.colorSizeTitle.colorName
                                        , colorItem.color, product.SKUJD.colorSizeTitle.sizeName, colorItem.size);
                                }
                                else if (product.SKUJD.colorSizeTitle.sizeName == null && product.SKUJD.colorSizeTitle.colorName != null)
                                {
                                    name = string.Format("{0}:{1}", product.SKUJD.colorSizeTitle.colorName, colorItem.color);
                                }
                                product.product_property += string.Format(propertyFormat, index++, name, product.ProductPrice, colorImg, _radio_num, (index == 1).ToString().ToLower());
                                AppendLog(txtLog, "第[{0}]个SKU=[{1}]处理完成...", index + 1, name);
                            }
                        }
                        break;
                    case "天猫":
                        {
                            var skuList = product.SKUTmall;
                            string colorImg = product.ThumImg.LastOrDefault();
                            if (skuList == null || skuList.Count == 0)
                            {
                                product.product_property += string.Format(propertyFormat, index++, name, product.ProductPrice, colorImg, _radio_num, (index == 1).ToString().ToLower());
                                AppendLog(txtLog, "没有抓取到SKU，生成默认SKU=[{0}]处理完成...", name);
                            }
                            else
                            {
                                switch (skuList.Count)
                                {
                                    case 1:
                                        foreach (var sku in skuList[0].values)
                                        {
                                            if (!sku.image.IsNullOrEmpty())
                                            {
                                                colorImg = sku.image;
                                            }
                                            colorImg = await _context.ImageService.UploadImg(colorImg, 3);
                                            name = string.Format("{0}:{1}", skuList[0].text, sku.text);
                                            product.product_property += string.Format(propertyFormat, index++, name, product.ProductPrice, colorImg, _radio_num, (index == 1).ToString().ToLower());
                                            AppendLog(txtLog, "第[{0}]个SKU=[{1}]处理完成...", index + 1, name);
                                        }
                                        break;
                                    case 2:
                                        foreach (var sku1 in skuList[1].values)
                                        {
                                            if (!sku1.image.IsNullOrEmpty())
                                            {
                                                colorImg = sku1.image;
                                            }
                                            colorImg = await _context.ImageService.UploadImg(colorImg, 3);
                                            foreach (var sku0 in skuList[0].values)
                                            {
                                                name = string.Format("{0}:{1} | {2}:{3}",
                                                    skuList[1].text, sku1.text, skuList[0].text, sku0.text);
                                                product.product_property += string.Format(propertyFormat, index++, name, product.ProductPrice, colorImg, _radio_num, (index == 1).ToString().ToLower());
                                                AppendLog(txtLog, "第[{0}]个SKU=[{1}]处理完成...", index + 1, name);
                                            }
                                        }
                                        break;
                                    case 3:
                                    case 4:
                                        product.product_property += string.Format(propertyFormat, index++, name, product.ProductPrice, colorImg, _radio_num, (index == 1).ToString().ToLower());
                                        AppendLog(txtLog, "第[{0}]个SKU=[{1}]处理完成...", index + 1, name);
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("生成SKU出错，错误信息：{0}", ex.Message));
            }
            AppendLog(txtLog, "SKU处理完成...");
            #endregion

            #region 处理详情
            AppendLog(txtLog, "开始处理详情...");
            try
            {
                await Task.Delay(500);
                if (product.product_details.IsNullOrEmpty())
                {
                    if (product.DetailImg == null || product.DetailImg.Count == 0)
                    {
                        product.product_details = string.Empty;
                    }
                    else
                    {
                        List<string> detailList = new List<string>();
                        index = 1;
                        foreach (var item in product.DetailImg)
                        {
                            detailList.Add(string.Format("<p></p><img src=\"{0}\" />",
                                await _context.ImageService.UploadImg(item, 2)));
                            AppendLog(txtLog, "第[{0}]张详情图片护理完成...", index++);
                        }
                        product.product_details = detailList.Join("");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("处理详情出错，错误信息：{0}", ex.Message));
                return;
            }
            #endregion

            #region 处理本地参数
            product.product_type = _classId;
            AppendLogWarning(txtLog, "尝试自动识别发货地...");
            if (!product.DiscernLcation())
            {
                AppendLog(txtLog, "识别失败，使用系统设置...");
                product.region_code = _regionCode;
                product.region_name = _regionName;
            }
            else
            {
                AppendLog(txtLog, "识别成功，当前产品发货地已更改为：{0}", product.region_name);
            }
            product.new_old = _new_old;
            product.is_virtual = Convert.ToInt32(_is_virtual);
            product.product_ispostage = _product_ispostage;
            product.refund = _refund;
            product.Postage = _postage;
            product.Append = _append;
            product.Limit = _limit;
            product.product_type_service = _typeService;
            product.product_brand = tbBrand.Text.Trim();  //品牌
            product.share = tbShare.Text.Trim(); //分享
            #endregion

            #region 开始上传商品
            AppendLog(txtLog, "开始上传商品...");
            try
            {
                var productUploadResult = await _context.ProductService.UploadProduct(product);

                if (productUploadResult.success)
                {
                    AppendLogWarning(txtLog, "上传成功，商品id={0}...", productUploadResult.msg);
                }
                else
                {
                    AppendLogError(txtLog, "上传失败，原因：{0}...", productUploadResult.msg);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("上传失败，异常信息：{0}", ex.Message));
            }
            #endregion
        }

        /// <summary>
        /// 正店抓取
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        async Task ProcessStoreUrl(string url)
        {
            AppendLog(txtLog, "开始嗅探店铺商品...");
            ProductResult currentProduct = new ProductResult();
            var list = await GetAllProductsByUrl(url);
            AppendLog(txtLog, "该店铺共嗅探到商品{0}个...", list.Count);

            int index = 1;
            foreach (var item in list)
            {
                AppendLog(txtLog, "开始处理商品{0}...", item.SourceUrl);
                stStatus.Text = string.Format("正在处理第{0}个商品", index++);
                try
                {
                    currentProduct = await _context.UrlConvertProductService.ProcessProduct(item.SourceUrl);
                    await ProcessProductAndUpload(currentProduct);
                }
                catch (Exception ex)
                {
                    FailProducts.Add(currentProduct.SourceUrl, currentProduct);
                    AppendLogError(txtLog, "商品{0}处理失败[已添加到失败队列],等待1秒处理下一个商品,原因：{1}...", currentProduct.SourceUrl, ex.Message);
                    Thread.Sleep(1000);
                    break;
                }
                AppendLog(txtLog, "商品{0}处理完成,等待1秒处理下一个商品...", currentProduct.SourceUrl);
                Thread.Sleep(1000);
            }
        }
        #endregion

    }
}
