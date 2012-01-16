﻿namespace DD4T.Mvc.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System.Web;
    using System.Xml.Linq;
    using DD4T.ContentModel.Factories;
    using System.Collections;
    using DD4T.Mvc.Caching;
    using DD4T.Factories;
    using System.Diagnostics;
    using System.Configuration;
    using DD4T.Utils;

    public class TridionSiteMapProvider : StaticSiteMapProvider
    {

        private int debugCounter = 0;

          // Sitemap attributes which are not considered part of route, you can add your custom attributes here.
        private string[] _excludedAttributes = { "title", "description", "roles", "page", "topmenuposition" };
        public static readonly string DefaultCacheKey = "rootNode";

        private bool ShouldResolveComponentLinks { get; set; }
        private int CacheTime { get; set; }
        private int PollTime { get; set; }
        private IPageFactory _pageFactory = null;
        public virtual IPageFactory PageFactory {
            get
            {
                if (_pageFactory == null)
                    _pageFactory = new PageFactory();
                return _pageFactory;
            }
            set
            {
                _pageFactory = value;
            }
        }
        public virtual ILinkFactory LinkFactory { get; set; }
        public Dictionary<string, SiteMapNode> NodeDictionary { get; set; }

        public virtual string SiteMapPath
        {
            get
            {
                if (ConfigurationManager.AppSettings.AllKeys.Contains<string>("SitemapPath"))
                {
                    return ConfigurationManager.AppSettings["SitemapPath"];
                }
                return "/system/sitemap/sitemap.xml";
            }
        }


        private SiteMapNode ReadSitemapFromXml(string sitemapUrl)
        {
            SiteLogger.Debug(">>ReadSitemapFromXml", LoggingCategory.Performance);
            SiteMapNode rootNode = null;
            NodeDictionary = new Dictionary<string, SiteMapNode>();

            string sitemap;
            if (!PageFactory.TryFindPageContent(sitemapUrl, out sitemap))
            {
                sitemap = emptySiteMapString();
            }
            SiteLogger.Debug(string.Format("loaded sitemap with url {0}, length {1}", sitemapUrl, sitemap.Length), LoggingCategory.Performance);
 
            XDocument xDoc = XDocument.Parse(sitemap);

            SiteLogger.Debug("parsed sitemap into XDocument", LoggingCategory.Performance);

            //XElement siteMapRoot = xDoc.Element("siteMap");
            XElement siteMapRoot = xDoc.Root;


            try
            {
                rootNode = new TridionSiteMapNode(this, String.Empty, "root_" + PageFactory.PageProvider.PublicationId, String.Empty, String.Empty, String.Empty, new ArrayList(), new NameValueCollection(), new NameValueCollection(), String.Empty);
                SiteLogger.Debug("created root node",LoggingCategory.Performance);
                AddNode(rootNode);
                SiteLogger.Debug("added root node", LoggingCategory.Performance);

                //Fill down the hierarchy.
                AddChildren(rootNode, siteMapRoot.Elements(), 1);
            }
            catch (Exception e)
            {
                Exception e2 = e;
            }
            SiteLogger.Debug("<<ReadSitemapFromXml", LoggingCategory.Performance);
            return rootNode;
        }

        //protected override void AddNode(SiteMapNode node, SiteMapNode parentNode)
        //{
        //    parentNode.ChildNodes.Add(node);
        //}

        private void AddChildren(SiteMapNode rootNode, IEnumerable<XElement> siteMapNodes, int currentLevel)
        {
            SiteLogger.Debug(">>AddChildren for root node {0} at level {1}", LoggingCategory.Performance, rootNode.Title, currentLevel);
            foreach (var element in siteMapNodes)
            {
                SiteLogger.Debug(">>>for loop iteration: {0}", LoggingCategory.Performance, element.ToString());
                TridionSiteMapNode childNode;


                SiteLogger.Debug("about to create NameValueCollection", LoggingCategory.Performance);
                var attributes = new NameValueCollection();
                SiteLogger.Debug("finished creating NameValueCollection", LoggingCategory.Performance);

                foreach (var a in element.Attributes())
                {
                    SiteLogger.Debug("about to add attribute {0} to NVC", LoggingCategory.Performance, a.Name);
                    attributes.Add(a.Name.ToString(), a.Value);
                    SiteLogger.Debug("finished adding attribute {0} to NVC", LoggingCategory.Performance, a.Name);
                }
                string uri;
                // for backwards compatibility, the obsolete 'pageId' attribute is supported as synonym for 'uri'
                try
                {
                    SiteLogger.Debug("about to retrieve uri", LoggingCategory.Performance);
                    if (element.Attribute("uri") != null)
                        uri = element.Attribute("uri").Value;
                    else if (element.Attribute("pageId") != null)
                        uri = element.Attribute("pageId").Value;
                    else
                        uri = "";
                    SiteLogger.Debug("finished retrieving uri", LoggingCategory.Performance);
                    uri = uri ?? "";
                }
                catch
                {
                    SiteLogger.Debug("exception while retrieving uri", LoggingCategory.Performance);
                    uri = "";
                }

                SiteLogger.Debug("about to create TridionSiteMapNode", LoggingCategory.Performance);
                childNode = new TridionSiteMapNode(this,
                    element.Attribute("id").Value, //key
                    uri,
                    element.Attribute("url").Value, //url
                    element.Attribute("title").Value, //title
                    element.Attribute("description").Value, //description
                    null, //roles
                    attributes, //attributes
                    null, //explicitresourceKeys
                    null) { Level = currentLevel }; // implicitresourceKey
                SiteLogger.Debug("finished creating TridionSiteMapNode", LoggingCategory.Performance);

                SiteLogger.Debug("about to add TridionSiteMapNode to node dictionary", LoggingCategory.Performance);
                NodeDictionary.Add(childNode.Key, childNode);
                SiteLogger.Debug("finished adding TridionSiteMapNode to node dictionary", LoggingCategory.Performance);

                //Use the SiteMapNode AddNode method to add the SiteMapNode to the ChildNodes collection
                SiteLogger.Debug("about to add node to SiteMap", LoggingCategory.Performance);
                AddNode(childNode, rootNode);
                SiteLogger.Debug(string.Format("finished adding node to sitemap (title={0}, parent title={1})", childNode.Title, rootNode.Title), LoggingCategory.Performance);

                // Check for children in this node.
                AddChildren(childNode, element.Elements(), currentLevel + 1);
                SiteLogger.Debug("<<<for loop iteration: {0}", LoggingCategory.Performance, element.ToString());
            }
            
            SiteLogger.Debug("<<AddChildren for root node {0} at level {1}", LoggingCategory.Performance, rootNode.Title, currentLevel);
        }

        public virtual bool IsInitialized
        {
            get;
            private set;
        }

        public override void Initialize(string name, NameValueCollection attributes) 
        {
            if (!IsInitialized)
            {
                base.Initialize(name, attributes);

                CacheTime = Int32.Parse(attributes["cacheTime"]);
                PollTime = Int32.Parse(attributes["pollTime"]);


                //if (! PageFactory.HasPageChanged(SitemapUrl))
                //{
                //    return;
                //}
                IsInitialized = true;
            }
        }

        public virtual string CacheKey
        {
            get
            {
                return DefaultCacheKey;
            }
        }

        public virtual SitemapCacheDependency GetSitemapCacheDependency (int PollTime, string SiteMapPath)
        {
            return new SitemapCacheDependency(PollTime, SiteMapPath, PageFactory);
        }

        public override SiteMapNode BuildSiteMap()
        {
            SiteMapNode rootNode;
            debugCounter++;
            lock (this) //KT: i don't think this is needed. 
            {
                rootNode = HttpContext.Current.Cache[CacheKey] as SiteMapNode;
                if (rootNode == null)
                {
                    base.Clear();
                    rootNode = ReadSitemapFromXml(SiteMapPath);
                    if (rootNode == null) return rootNode; //TODO: Review errorHandling
                    // Store the root node in the cache.
                    HttpContext.Current.Cache.Insert(CacheKey, rootNode, GetSitemapCacheDependency(PollTime, SiteMapPath));
                }
            }
            return rootNode;
        }

        private string emptySiteMapString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<siteMap publicationid=\"tcm:0-70-1\">");
            sb.Append("<siteMapNode title=\"website\" url=\"/\">");
            sb.Append("</siteMapNode>");
            sb.Append("</siteMap>");

            return sb.ToString();
        }

        protected override SiteMapNode GetRootNodeCore()
        {
            return BuildSiteMap();
        }

        public override SiteMapNode RootNode
        {
            get
            {
                return BuildSiteMap();
            }
        }

        protected override void Clear()
        {
            lock (this)
            {
                HttpContext.Current.Cache.Remove("rootNode");
                base.Clear();
            }
        }

    }
}