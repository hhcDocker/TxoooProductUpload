﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TxoooProductUpload.Service
{
    /// <summary>
    /// 服务状态上下文
    /// </summary>
    class ServiceContext
    {
        public ServiceContext()
        {
            Session = new Session();
            UrlConvertProductService = new UrlConvertProductService();
            //OrderQueueService = new OrderQueueService(this);
            //OrderSubmitService = new OrderSubmitService(this);
            //PassengerService = new PassengerService(this);
            //StationDataService = new StationDataService(this);
            //VerifyCodeService = new VerifyCodeService(this);
            //TicketQueryService = new TicketQueryService(this);
            //DataService = new DataService(this);
        }

        /// <summary>
        /// 获得当前的会话状态
        /// </summary>
        public Session Session { get; private set; }

        /// <summary>
        /// 获得当前解析商品服务
        /// </summary>
        public UrlConvertProductService UrlConvertProductService { get; private set; }


        ///// <summary>
        ///// 获得当前的订单排队服务
        ///// </summary>
        //public OrderQueueService OrderQueueService { get; private set; }

        ///// <summary>
        ///// 获得当前的订单提交服务
        ///// </summary>
        //public OrderSubmitService OrderSubmitService { get; private set; }

        ///// <summary>
        ///// 获得当前的联系人服务
        ///// </summary>
        //public PassengerService PassengerService { get; private set; }

        ///// <summary>
        ///// 获得当前的车站数据服务
        ///// </summary>
        //public StationDataService StationDataService { get; private set; }

        ///// <summary>
        ///// 获得当前的验证码服务
        ///// </summary>
        //public VerifyCodeService VerifyCodeService { get; private set; }

        ///// <summary>
        ///// 获得当前的查票服务
        ///// </summary>
        //public TicketQueryService TicketQueryService { get; private set; }

        ///// <summary>
        ///// 数据服务
        ///// </summary>
        //public DataService DataService { get; private set; }
    }
}
