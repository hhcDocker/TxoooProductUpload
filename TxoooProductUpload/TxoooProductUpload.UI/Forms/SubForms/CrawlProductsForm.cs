using CCWin;
using CCWin.SkinControl;
using HtmlAgilityPack;
using System;
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
using TxoooProductUpload.UI.Service.Entities;

namespace TxoooProductUpload.UI.Forms.SubForms
{

    /// <summary>
    /// ץȡ��Ʒ
    /// </summary>
    public partial class CrawlProductsForm : Form
    {
        UserControls.ProcessProduct _process1;
        UserControls.ProcessProductResult _processResult;

        bool _isAuto = false;  //�Զ�ץȡȫ���б�
        ProductHelper _productHelper = new ProductHelper();

        List<ProductSourceInfo> _productList = new List<ProductSourceInfo>();  //��һ��ץȡ��Ʒ����Ϣ
        List<ProductSourceInfo> _productOkList = new List<ProductSourceInfo>();   //����ץȡ�ɹ�����Ʒ��Ϣ

        CrawlType _crawlType = CrawlType.None;
        ///// <summary>
        ///// ��ǰҳ���html
        ///// </summary>
        //public string Html { set; get; }


        public CrawlProductsForm()
        {
            InitializeComponent();

            Load += CrawlProductsForm_Load;

            _process1 = new UserControls.ProcessProduct();
            _process1.Dock = DockStyle.Fill;
            _process1.Visible = false;
            this.Controls.Add(_process1);

            _processResult = new UserControls.ProcessProductResult();
            _processResult.Dock = DockStyle.Fill;
            _processResult.Visible = false;
            this.Controls.Add(_processResult);
        }

        private void CrawlProductsForm_Load(object sender, EventArgs e)
        {
            InitMenuEvent();
            InitDgv();
            InitControlBtnEvent();

            bs.DataSource = _productList;

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
                CrawProduct();
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

        void ProcessProductDetailResult()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    ProcessProductDetailResult();
                })); return;
            }
            _processResult.ProductBindSource.DataSource =null;
            _processResult.ProductBindSource.DataSource = _productOkList;
            _processResult.MessageShowLable.Text = "��{0}����Ʒ��ִ�гɹ�{1}����Ʒ".FormatWith(_productList.Count, _productOkList.Count);
            _process1.Visible = skinSplitContainer1.Visible = false;
            tssBtnNext.Enabled = tssBtnPrevious.Enabled = _processResult.Visible = true;
        }

        /// <summary>
        /// ��һ������
        /// </summary>
        void NextProcess()
        {
            Task.Run(() =>
            {
                var waitProcessProductList = _productList.Where(m => m.IsProcess == false).ToList();
                if (waitProcessProductList.Count > 0)
                {
                    Invoke(new Action(() =>
                    {
                        tssBtnNext.Enabled = skinSplitContainer1.Visible = false;
                        _process1.Visible = true;
                        _process1.ProcessBar.Maximum = waitProcessProductList.Count;
                        _process1.ProcessBar.Value = _process1.ProcessBar.Minimum = 0;
                    })); 
                    Parallel.For(0, waitProcessProductList.Count, (index) =>
                    {
                        var product = waitProcessProductList[index];
                        if (product.IsProcess) return;
                        try
                        {
                            _productHelper.ProcessItem(ref product);
                            App.Context.ProductService.DiscernLcation(ref product);
                            _productOkList.Add(product);
                        }
                        catch (Exception ex)
                        {
                            Iwenli.LogHelper.LogError(this, "{0}��Ʒ{1}�쳣��{2}".FormatWith(product.SourceName, product.Id, ex.Message));
                        }
                        Invoke(new Action(() =>
                        {
                            _process1.ProcessBar.Value += 1;
                        }));
                    });
                    ProcessProductDetailResult();
                }
                else
                {
                    ProcessProductDetailResult();
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
                CrawProduct();
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
            CrawProduct();
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
        /// <summary>
        /// ץȡ��Ʒ
        /// </summary>
        void CrawProduct()
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
                        if (_productList.Exists(m => m.Id == item.Id)) { continue; }
                        bs.Add(item);
                    }

                    sdgvProduct.FirstDisplayedScrollingRowIndex = sdgvProduct.Rows.Count - 1;
                    if (_isAuto)
                    {
                        //ѭ�������һҳ  �˳�ѭ��
                        if (document.DocumentNode.SelectNodes("//span[@class='icon icon-btn-next-2-disable']") != null)
                        {
                            _isAuto = false;
                            MessageBoxEx.Show("ץȡ��ϣ���ץȡ��Ʒ{0}��".FormatWith(sdgvProduct.Rows.Count));
                            return;
                        }
                        NextPageList();
                    }
                }));
            });
        }
        #endregion

        /// <summary>
        /// ���������һҳ
        /// </summary>
        void NextPageList()
        {
            webBrowser.Browser.GetMainFrame().ExecuteJavaScript("document.getElementsByClassName('icon-btn-next-2')[0].click()", "", 0);
        }
    }
}