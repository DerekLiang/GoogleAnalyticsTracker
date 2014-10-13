using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using GoogleAnalyticsTracker.Core;

namespace GoogleAnalyticsTracker.WebApi2 {
    public class ActionTrackingAttribute
            : AsyncActionFilterAttribute
    {
        private Func<HttpActionContext, bool> _isTrackableAction;

        public Tracker Tracker { get; set; }

        TrackingArgument _trackingArgument;


        public Func<HttpActionContext, bool> IsTrackableAction
        {
            get
            {
                if (_isTrackableAction != null)
                {
                    return _isTrackableAction;
                }
                return action => true;
            }
            set { _isTrackableAction = value; }
        }

        public string ActionDescription { get; set; }
        public string ActionUrl { get; set; }

        public ActionTrackingAttribute()
            : this(null, null, null, null)
        {
        }

        public ActionTrackingAttribute(string trackingAccount, string trackingDomain)
            : this(trackingAccount, trackingDomain, null, null)
        {
        }

        public ActionTrackingAttribute(string trackingAccount)
            : this(trackingAccount, null, null, null)
        {
        }

        public ActionTrackingAttribute(string trackingAccount, string trackingDomain, string actionDescription, string actionUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(trackingDomain) && System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.Request != null)
                {
                    trackingDomain = System.Web.HttpContext.Current.Request.Url.Host;
                }
            }
            catch
            {
                // intended
            }

            Tracker = new Tracker(trackingAccount, trackingDomain, new CookieBasedAnalyticsSession(), new AspNetWebApiTrackerEnvironment());
            ActionDescription = actionDescription;
            ActionUrl = actionUrl;
        }

        public ActionTrackingAttribute(Tracker tracker)
            : this(tracker, action => true)
        {
        }

        public ActionTrackingAttribute(Tracker tracker, Func<HttpActionContext, bool> isTrackableAction)
        {
            Tracker = tracker;
            IsTrackableAction = isTrackableAction;
        }

        public ActionTrackingAttribute(string trackingAccount, string trackingDomain, Func<HttpActionContext, bool> isTrackableAction)
        {
            Tracker = new Tracker(trackingAccount, trackingDomain, new CookieBasedAnalyticsSession(), new AspNetWebApiTrackerEnvironment());
            IsTrackableAction = isTrackableAction;
        }

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            if (IsTrackableAction(actionContext))
            {
                var requireRequestAndResponse = Tracker.AnalyticsSession as IRequireRequestAndResponse;
                if (requireRequestAndResponse != null)
                {
                    requireRequestAndResponse.SetRequestAndResponse(actionContext.Request, actionContext.Response);
                }

                PrepareTrackingArgument(actionContext).Start();
            }
        }

        public override async Task OnActionExecutedAsync(HttpActionExecutedContext actionExecutedContext, CancellationToken cancellationToken)
        {
            if (_trackingArgument != null)
            {
                await OnTrackingAction(_trackingArgument.Stop());
            }
        }

        public virtual string BuildCurrentActionName(HttpActionContext filterContext)
        {
            if (!string.IsNullOrEmpty(ActionDescription))
            {
                return ActionDescription;
            } 
            else if (filterContext.ActionDescriptor.ControllerDescriptor != null)
            {
                return filterContext.ActionDescriptor.ControllerDescriptor.ControllerName + " - " +
                       filterContext.ActionDescriptor.ActionName;
            }
            else
            {
                return filterContext.ControllerContext.ControllerDescriptor.ControllerName + " - " +
                         filterContext.ActionDescriptor.ActionName;
            }
        }

        public virtual string BuildCurrentActionUrl(HttpActionContext filterContext)
        {
            var request = filterContext.Request;

            return ActionUrl ?? (request.RequestUri != null ? request.RequestUri.PathAndQuery : "");
        }

        public virtual TrackingArgument PrepareTrackingArgument(HttpActionContext filterContext)
        {
            _trackingArgument = new TrackingArgument() {
                Request= filterContext.Request,
                ActionName = BuildCurrentActionName(filterContext),
                ActionUrl = BuildCurrentActionUrl(filterContext),
                Beacons = new Dictionary<string, string>(),
            };
            return _trackingArgument;
        }

        public virtual async Task<TrackingResult> OnTrackingAction(TrackingArgument trackingArgument)
        {
            return await Tracker.TrackPageViewAsync(
                trackingArgument.Request,
                trackingArgument.ActionName,
                trackingArgument.ActionUrl,
                trackingArgument.Beacons);
        }
    }

    public class TrackingArgument
    {
        public TrackingArgument()
        {
            Stopwatch = new Stopwatch();
            Beacons = new Dictionary<string, string>();
        }

        public void Start()
        {
            this.Stopwatch.Start();
        }

        public TrackingArgument Stop()
        {
            this.Stopwatch.Stop();
            Beacons.Add("timingValue", this.Stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            Beacons.Add("timingCategory", this.ActionName);
            Beacons.Add("timingVar", this.ActionUrl);
            Beacons.Add("timingLabel", "Web API Execution Time");
            return this;
        }

        public Stopwatch Stopwatch { get; set; }

        public HttpRequestMessage Request { get; set; }

        public string ActionName { get; set; }

        public string ActionUrl { get; set; }

        public Dictionary<string, string> Beacons { get; set; }
    }
}