using CCWin;
using CCWin.SkinControl;
using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TxoooProductUpload.Entities.Product;
using TxoooProductUpload.Service.Crawl;
using TxoooProductUpload.UI.CefGlue.Browser;
using TxoooProductUpload.UI.Common;
using TxoooProductUpload.UI.Common.Const;
using TxoooProductUpload.UI.Common.Extended.Winform;
using TxoooProductUpload.UI.Service.Cache.ProductCache;
using TxoooProductUpload.UI.Service.Entities;

namespace TxoooProductUpload.UI.Forms.SubForms
{

    /// <summary>
    /// ץȡ��Ʒ
    /// </summary>
    public partial class CrawlProductsForm : Form
    {
        #region ����
        UserControls.ProcessProduct _process1;
        UserControls.ProcessProductResult _processResult;

        int _threadCount = 5;   //���������߳��� 
        bool _isAuto = false;  //�Զ�ץȡȫ���б�
        ProductHelper _productHelper = new ProductHelper(App.Context.BaseContent.ImageService);
        CrawlType _crawlType = CrawlType.None;
        #endregion

        #region ����
        /// <summary>
        /// ��Ʒ����
        /// </summary>
        ProductCacheData ProductCache { set; get; } 
        #endregion

        public CrawlProductsForm()
        {
            InitializeComponent();
            ProductCacheContext.Instance.Init();
            ProductCache = ProductCacheContext.Instance.Data;

            Load += CrawlProductsForm_Load;

            #region �����������
            _process1 = new UserControls.ProcessProduct();
            _process1.Dock = DockStyle.Fill;
            _process1.Visible = false;
            this.Controls.Add(_process1);

            _processResult = new UserControls.ProcessProductResult();
            _processResult.Dock = DockStyle.Fill;
            _processResult.Visible = false;
            this.Controls.Add(_processResult);
            #endregion
        }

        private void CrawlProductsForm_Load(object sender, EventArgs e)
        {
            InitMenuEvent();
            InitDgv();
            InitControlBtnEvent();

            bs.DataSource = ProductCache.WaitProcessList;

            tsBtnAutoAll.Click += TsBtnAutoAll_Click;
            tssBtnBatchDel.Click += (_s, _e) => { DeleteRows(); };
            tsTxtUrl.TextChanged += TsTxtUrl_TextChanged;
        }

        private void TsBtnAutoAll_Click(object sender, EventArgs e)
        {
            //NextPageList();
            if (tsBtnAutoAll.Text == "�Զ�(&A)")
            {
                _isAuto = true;
                tsBtnAutoAll.Text = "��ͣ(&S)";
                CrawProductBase();
            }
            else
            {
                _isAuto = false;
                tsBtnAutoAll.Text = "�Զ�(&A)";
            }
        }

        #region ҳ����ʱ����
        void TsTxtUrl_TextChanged(object sender, EventArgs e)
        {
            var url = tsTxtUrl.Text.Trim();
            if (url.IndexOf("s.taobao.com/search") > 1)
            {
                _crawlType = CrawlType.TaoBaoSearch;
            }
            else if (url.IndexOf("item.taobao.com") > 1)
            {
                _crawlType = CrawlType.TaoBaoItem;
            }

            switch (_crawlType)
            {
                case CrawlType.None:
                    tsBtnAutoAll.Enabled = tsBtnOneProduct.Enabled = tsBtnPageProducts.Enabled = false;
                    break;
                case CrawlType.TaoBaoSearch:
                    tsBtnAutoAll.Enabled = tsBtnPageProducts.Enabled = true;
                    break;
                case CrawlType.TaoBaoItem:
                    tsBtnOneProduct.Enabled = true;
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region �ײ����Ʋ˵����
        void InitControlBtnEvent()
        {
            tssBtnNext.Click += ControlBtn_Click;
            tssBtnPrevious.Click += ControlBtn_Click;
        }


        void ControlBtn_Click(object sender, EventArgs e)
        {
            ToolStripStatusLabel current = sender as ToolStripStatusLabel;
            switch (current.Tag.ToString())
            {
                case "prev":
                    Previous();
                    break;
                case "next":
                    NextProcess();
                    break;
            }
        }

        /// <summary>
        /// ��һ��
        /// </summary>
        void Previous()
        {
            ProductCache.WaitProcessList.AddRange(ProductCache.ProcessFailList);
            ProductCache.ProcessFailList.Clear();
            bs.DataSource = null;
            bs.DataSource = ProductCache.WaitProcessList;
            tssBtnNext.Enabled = skinSplitContainer1.Visible = true;
            _process1.Visible = tssBtnPrevious.Enabled = false;
        }

        /// <summary>
        /// ������Ʒ����
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        void ProcessProductDetail(List<ProductSourceInfo> list)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    ProcessProductDetail(list);
                }));
                return;
            }
        }

        void ProcessProductDetailResult(int allCount, int successCount)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    ProcessProductDetailResult(allCount, successCount);
                })); return;
            }
            _processResult.ProductBindSource.DataSource = null;
            _processResult.ProductBindSource.DataSource = ProductCache.WaitUploadImageList;
            _processResult.MessageShowLable.Text = "���ι�����{0}����Ʒ������ɹ�{1}����Ʒ���Ѿ�׷�ӵ�������"
                .FormatWith(allCount, successCount);
            _process1.Visible = skinSplitContainer1.Visible = false;
            _processResult.Visible = true;
            //tssBtnNext.Enabled = tssBtnPrevious.Enabled = 

            ProductCacheContext.Instance.Save();
        }

        /// <summary>
        /// ��һ������
        /// </summary>
        void NextProcess()
        {
            Task.Run(() =>
           {
               var allCount = ProductCache.WaitProcessList.Count;
               if (allCount > 0)
               {
                   Invoke(new Action(() =>
                   {
                       tssBtnNext.Enabled = skinSplitContainer1.Visible = false;
                       _process1.Visible = true;
                       _process1.ProcessBar.Maximum = ProductCache.WaitProcessList.Count;
                       _process1.ProcessBar.Value = _process1.ProcessBar.Minimum = 0;
                   }));
                   var cts = new CancellationTokenSource();
                   var tasks = new Task[_threadCount];
                   for (int i = 0; i < _threadCount; i++)
                   {
                       tasks[i] = new Task(() => GrabDetailTask(cts.Token), cts.Token, TaskCreationOptions.LongRunning);
                       tasks[i].Start();
                   }
                   Task.Run(async () =>
                   {
                       while (true)
                       {
                           lock (ProductCache.WaitProcessList)
                           {
                               if (allCount == ProductCache.WaitUploadImageList.Count + ProductCache.ProcessFailList.Count)
                               {
                                   ProcessProductDetailResult(allCount, ProductCache.WaitUploadImageList.Count);
                                   break;
                               }
                           }
                           await Task.Delay(1000);
                       }
                   });
               }
               else
               {
                   ProcessProductDetailResult(0, 0);
               }
           });
        }

        /// <summary>
        /// ɾ��ѡ����
        /// </summary>
        void DeleteRows()
        {
            var rows = GetSelectRow();
            if (rows != null)
            {
                foreach (var item in rows)
                {
                    sdgvProduct.Rows.Remove(item);
                }
            }
        }

        /// <summary>
        /// ��ȡѡ�е��� ��ѡ�з���null
        /// </summary>
        /// <returns></returns>
        List<DataGridViewRow> GetSelectRow()
        {
            sdgvProduct.EndEdit();
            var selRows = sdgvProduct.Rows.OfType<DataGridViewRow>().Where(m => m.Cells[0].Value != null && m.Cells[0].Value.ToString() == "True").ToList();
            if (selRows.Count == 0)
            {
                MessageBoxEx.Show("��ѡ��Ҫ��������Ʒ", AppInfo.AssemblyTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            return selRows;
        }
        #endregion

        #region ��Ʒչʾ������  ��һ���õ�
        void InitDgv()
        {
            InitDgvAllSelect();
            sdgvProduct.CellContentClick += SdgvProduct_CellContentClick;
            sdgvProduct.DataError += (s, e) => { };  //��дDataError�¼�
            sdgvProduct.SelectionChanged += (_s, _e) =>
            {
                tssBtnNext.Enabled = sdgvProduct.Rows.Count > 0;
            };
        }
        /// <summary>
        /// ��Ԫ�񵥻�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void SdgvProduct_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewCell cell = sdgvProduct.Rows[e.RowIndex].Cells[e.ColumnIndex];
                if (sdgvProduct.Columns[e.ColumnIndex] == Delete)
                {
                    //ɾ��
                    sdgvProduct.Rows.Remove(sdgvProduct.Rows[e.RowIndex]);
                }
                if (sdgvProduct.Columns[e.ColumnIndex] == ShowPhone)
                {
                    Utils.OpenUrl(sdgvProduct.Rows[e.RowIndex].Cells["h5UrlDataGridViewTextBoxColumn"].Value.ToString(), true);
                }
                if (sdgvProduct.Columns[e.ColumnIndex] == ShowPc)
                {
                    Utils.OpenUrl(sdgvProduct.Rows[e.RowIndex].Cells["urlDataGridViewTextBoxColumn"].Value.ToString());
                }
            }
        }

        void InitDgvAllSelect()
        {
            DataGridViewCheckBoxColumn colCB = new DataGridViewCheckBoxColumn();
            DataGridViewCheckBoxHeaderCell cbHeader = new DataGridViewCheckBoxHeaderCell();
            colCB.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colCB.HeaderText = "ȫѡ";
            colCB.Width = 30;
            colCB.HeaderCell = cbHeader;
            sdgvProduct.Columns.Insert(0, colCB);
            cbHeader.OnCheckBoxClicked += CbHeader_OnCheckBoxClicked;
        }

        void CbHeader_OnCheckBoxClicked(object sender, datagridviewCheckboxHeaderEventArgs e)
        {
            sdgvProduct.EndEdit();
            sdgvProduct.Rows.OfType<DataGridViewRow>().ToList().ForEach(t => t.Cells[0].Value = e.CheckedState);
        }
        #endregion

        #region ���Ʋ˵�����¼�
        void InitMenuEvent()
        {
            tsBtnGO.Click += TsBtnGO_Click;
            tsBtnLeft.Click += TsBtnLeft_Click;
            tsBtnRight.Click += TsBtnRight_Click;
            tsBtnRefresh.Click += TsBtnRefresh_Click;
            tsTxtUrl.KeyUp += TsTxtUrl_KeyUp;
            webBrowser.AddressChanged += WebBrowser_AddressChanged;

            webBrowser.LoadEnd += WebBrowser_LoadEnd;

            tsBtnPageProducts.Click += TsBtnTest_Click;
        }


        void WebBrowser_LoadEnd(object sender, Xilium.CefGlue.WindowsForms.LoadEndEventArgs e)
        {
            //var url = tsTxtUrl.Text;
            //tsBtnAutoAll.Enabled = tsBtnPageProducts.Enabled = url.IndexOf("s.taobao.com/search") > 1;
        }

        void WebBrowser_AddressChanged(object sender, Xilium.CefGlue.WindowsForms.AddressChangedEventArgs e)
        {
            tsTxtUrl.Text = webBrowser.Browser.GetMainFrame().Url;
            tsBtnLeft.Enabled = webBrowser.Browser.CanGoBack;
            tsBtnRight.Enabled = webBrowser.Browser.CanGoForward;
            if (_isAuto)
            {
                Thread.Sleep(1000);
                CrawProductBase();
            }
        }


        /// <summary>
        /// ��ȡҳ��HTML
        /// </summary>
        /// <param name="callBack"></param>
        void HtmlChange(Action<string> callBack)
        {
            if (webBrowser.Browser.GetMainFrame().IsMain)
            {
                var visitor = new SourceVisitor(callBack);
                webBrowser.Browser.GetMainFrame().GetSource(visitor);
            }
        }

        void TsBtnTest_Click(object sender, EventArgs e)
        {
            CrawProductBase();
        }

        void TsBtnRefresh_Click(object sender, EventArgs e)
        {
            webBrowser.Browser.Reload();
        }

        void TsBtnRight_Click(object sender, EventArgs e)
        {
            webBrowser.Browser.GoForward();
        }

        void TsBtnLeft_Click(object sender, EventArgs e)
        {
            webBrowser.Browser.GoBack();
        }

        void TsTxtUrl_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Control || e.KeyCode == Keys.Enter)
            {
                OpenNewUrl(tsTxtUrl.Text.Trim());
            }
        }

        void TsBtnGO_Click(object sender, EventArgs e)
        {
            OpenNewUrl(tsTxtUrl.Text.Trim());
        }

        /// <summary>
        /// ��webBrowser�д���url
        /// </summary>
        /// <param name="url"></param>
        void OpenNewUrl(string url)
        {
            webBrowser.Browser.GetMainFrame().LoadUrl(tsTxtUrl.Text);
        }
        #endregion

        #region ץȡ��Ʒ
        #region ץȡ��Ʒ������Ϣ
        /// <summary>
        /// ץȡ��Ʒ������Ϣ
        /// </summary>
        void CrawProductBase()
        {
            HtmlChange(Html =>
            {
                BeginInvoke(new Action(() =>
                {
                    HtmlAgilityPack.HtmlDocument document = new HtmlAgilityPack.HtmlDocument();
                    document.LoadHtml(Html);

                    var list = _productHelper.GetProductListFormSearch(document, SourceType.Taobao);
                    foreach (var item in list)
                    {
                        if (IsEsists(item)) { continue; }
                        bs.Add(item);
                    }
                    if (sdgvProduct.Rows.Count > 8)
                    {
                        sdgvProduct.FirstDisplayedScrollingRowIndex = sdgvProduct.Rows.Count - 1;
                    }
                    if (_isAuto)
                    {
                        //ѭ�������һҳ  �˳�ѭ��
                        if (document.DocumentNode.SelectNodes("//span[@class='icon icon-btn-next-2-disable']") != null)
                        {
                            _isAuto = false;
                            MessageBoxEx.Show("ץȡ��ϣ���ץȡ��Ʒ{0}��".FormatWith(sdgvProduct.Rows.Count));
                            return;
                        }
                        //��һҳ
                        webBrowser.Browser.GetMainFrame().ExecuteJavaScript("document.getElementsByClassName('icon-btn-next-2')[0].click()", "", 0);
                    }
                }));
            });
        }
        #endregion

        #region ץȡ��ϸ��Ϣ
        /// <summary>
        /// ��ʼץȡ����ҳ
        /// </summary>
        async void GrabDetailTask(CancellationToken token)
        {
            try
            {
                ProductHelper productHelper = new ProductHelper(App.Context.BaseContent.ImageService);

                ProductSourceInfo task;
                while (!token.IsCancellationRequested)
                {
                    task = null;
                    lock (ProductCache.WaitProcessList)
                    {
                        if (ProductCache.WaitProcessList.Count > 0)
                        {
                            task = ProductCache.WaitProcessList[0];
                            ProductCache.WaitProcessList.Remove(task);
                        }
                    }
                    //û�����˳�
                    if (task == null)
                    {
                        break;
                    }
                    try
                    {
                        productHelper.ProcessItem(task);
                        App.Context.ProductService.DiscernLcation(task);
                    }
                    catch (Exception ex)
                    {
                        Iwenli.LogHelper.LogError(this,
                            "[����]{0}��Ʒ{1}�쳣��{2}".FormatWith(task.SourceName, task.Id, ex.Message));
                    }
                    if (task.IsProcess)
                    {
                        lock (ProductCache.ProcessFailList)
                        {
                            ProductCache.WaitUploadImageList.Add(task);

                            if (ProductCache.ProcessFailList.Count > 0 && ProductCache.ProcessFailList.Count % 20 == 0)
                            {
                                //ÿ�ɹ�20���ֶ��ͷ�һ���ڴ�
                                GC.Collect();
                                //�����������ݣ���ֹʲôʱ��崻���������Ȼع�̫��
                                ProductCacheContext.Instance.Save();
                            }
                        }
                    }
                    else
                    {
                        lock (ProductCache.ProcessFailList)
                        {
                            ProductCache.ProcessFailList.Add(task);
                        }
                    }

                    Invoke(new Action(() =>
                    {
                        var value = _process1.ProcessBar.Value + 1;
                        //Iwenli.LogHelper.LogDebug(this, value.ToString());
                        _process1.ProcessBar.Value = value;
                    }));
                    //�ȴ�һ���� ��ִ����һ��
                    await Task.Delay(1000, token);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }
        #endregion

        /// <summary>
        /// �ж���Ʒ�Ƿ�ץȡ��
        /// </summary>
        /// <returns></returns>
        bool IsEsists(ProductSourceInfo product)
        {
            //����ֻ�ӵ�ǰ�����ж�  �������Ӵ����ݿ��ж�
            if (ProductCache.WaitProcessList.FirstOrDefault(m => m.Id == product.Id) != null)
            {
                return true;
            }
            return false;
        }
        #endregion

    }
}