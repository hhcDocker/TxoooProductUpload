﻿using System;
using System.Collections.Generic;
using System.Text;
using Xilium.CefGlue;
using Xilium.CefGlue.WindowsForms;

namespace TxoooProductUpload.UI.CefGlue
{
    /// <summary>
    /// 用于监听页面加载状态，地址变化，标题等得信息
    /// </summary>
    internal sealed class WlCefDisplayHandler : CefDisplayHandler
    {
        private readonly  CefWebBrowser _core;

        public WlCefDisplayHandler( CefWebBrowser core)
        {
            _core = core;
        }

        protected override void  OnTitleChange(CefBrowser browser, string title)
        {
            _core.InvokeIfRequired(() => _core.OnTitleChanged(new TitleChangedEventArgs(title)));
        }

        protected override void OnAddressChange(CefBrowser browser, CefFrame frame, string url)
        {
            if (frame.IsMain)
            {
               _core.InvokeIfRequired(() => _core.OnAddressChanged(new AddressChangedEventArgs(frame, url)));
            }
        }

        protected override void OnStatusMessage(CefBrowser browser, string value)
        {
            _core.InvokeIfRequired(() => _core.OnStatusMessage(new StatusMessageEventArgs(value)));
        }

		protected override bool OnConsoleMessage(CefBrowser browser, string message, string source, int line)
		{
			var e = new ConsoleMessageEventArgs(message, source, line);
			_core.InvokeIfRequired(() => _core.OnConsoleMessage(e));

			return e.Handled;
		}

		protected override bool OnTooltip(CefBrowser browser, string text)
		{
			var e = new TooltipEventArgs(text);
			_core.InvokeIfRequired(()=> _core.OnTooltip(e));
			return e.Handled;
		}
    }
}
