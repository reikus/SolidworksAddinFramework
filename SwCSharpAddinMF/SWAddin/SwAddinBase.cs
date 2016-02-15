using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using SolidWorksTools;
using SolidWorksTools.File;
using Attribute = System.Attribute;

namespace SwCSharpAddinMF.SWAddin
{
    public abstract class SwAddinBase : ISwAddin
    {
        protected ICommandManager ICmdMgr;
        public int AddinId { get; private set; }
        protected BitmapHandler Bmp;
        private Hashtable openDocs = new Hashtable();
        private SldWorks SwEventPtr;

        public ISldWorks SwApp => ISwApp;

        public ICommandManager CmdMgr => ICmdMgr;

        public Hashtable OpenDocs => openDocs;

        public ISldWorks ISwApp { set; get; }

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            #region Get Custom Attribute: SwAddinAttribute
            SwAddinAttribute sWattr = null;
            Type type = typeof(SwAddin);

            foreach (Attribute attr in type.GetCustomAttributes(false))
            {
                if (attr is SwAddinAttribute)
                {
                    sWattr = attr as SwAddinAttribute;
                    break;
                }
            }

            #endregion

            try
            {
                RegistryKey hklm = Registry.LocalMachine;
                RegistryKey hkcu = Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID + "}";
                RegistryKey addinkey = hklm.CreateSubKey(keyname);
                addinkey.SetValue(null, 0);

                addinkey.SetValue("Description", sWattr.Description);
                addinkey.SetValue("Title", sWattr.Title);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID + "}";
                addinkey = hkcu.CreateSubKey(keyname);
                addinkey.SetValue(null, Convert.ToInt32(sWattr.LoadAtStartup), RegistryValueKind.DWord);
            }
            catch (NullReferenceException nl)
            {
                Console.WriteLine("There was a problem registering this dll: SWattr is null. \n\"" + nl.Message + "\"");
                MessageBox.Show("There was a problem registering this dll: SWattr is null.\n\"" + nl.Message + "\"");
            }

            catch (Exception e)
            {
                Console.WriteLine(e.Message);

                MessageBox.Show("There was a problem registering the function: \n\"" + e.Message + "\"");
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                RegistryKey hklm = Registry.LocalMachine;
                RegistryKey hkcu = Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID + "}";
                hklm.DeleteSubKey(keyname);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID + "}";
                hkcu.DeleteSubKey(keyname);
            }
            catch (NullReferenceException nl)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + nl.Message);
                MessageBox.Show("There was a problem unregistering this dll: \n\"" + nl.Message + "\"");
            }
            catch (Exception e)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + e.Message);
                MessageBox.Show("There was a problem unregistering this dll: \n\"" + e.Message + "\"");
            }
        }

        public bool ConnectToSW(object ThisSW, int cookie)
        {
            ISwApp = (ISldWorks)ThisSW;
            AddinId = cookie;

            //Setup callbacks
            ISwApp.SetAddinCallbackInfo(0, this, AddinId);

            #region Setup the Command Manager
            Connect();
            #endregion

            #region Setup the Event Handlers
            SwEventPtr = (SldWorks)ISwApp;
            openDocs = new Hashtable();
            AttachEventHandlers();
            #endregion


            return true;
        }

        public abstract void Connect();
        public abstract void Disconnect();

        public bool DisconnectFromSW()
        {
            Disconnect();
            DetachEventHandlers();

            Marshal.ReleaseComObject(ICmdMgr);
            ICmdMgr = null;
            Marshal.ReleaseComObject(ISwApp);
            ISwApp = null;
            //The addin _must_ call GC.Collect() here in order to retrieve all managed code pointers 
            GC.Collect();
            GC.WaitForPendingFinalizers();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }

        public bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            List<int> storedList = new List<int>(storedIDs);
            List<int> addinList = new List<int>(addinIDs);

            addinList.Sort();
            storedList.Sort();

            if (addinList.Count != storedList.Count)
            {
                return false;
            }
            for (int i = 0; i < addinList.Count; i++)
            {
                if (addinList[i] != storedList[i])
                {
                    return false;
                }
            }
            return true;
        }

        public bool AttachEventHandlers()
        {
            AttachSwEvents();
            //Listen for events on all currently open docs
            AttachEventsToAllDocuments();
            return true;
        }

        private bool AttachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveDocChangeNotify += OnDocChange;
                SwEventPtr.DocumentLoadNotify2 += OnDocLoad;
                SwEventPtr.FileNewNotify2 += OnFileNew;
                SwEventPtr.ActiveModelDocChangeNotify += OnModelChange;
                SwEventPtr.FileOpenPostNotify += FileOpenPostNotify;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        private bool DetachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveDocChangeNotify -= OnDocChange;
                SwEventPtr.DocumentLoadNotify2 -= OnDocLoad;
                SwEventPtr.FileNewNotify2 -= OnFileNew;
                SwEventPtr.ActiveModelDocChangeNotify -= OnModelChange;
                SwEventPtr.FileOpenPostNotify -= FileOpenPostNotify;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

        }

        public void AttachEventsToAllDocuments()
        {
            ModelDoc2 modDoc = (ModelDoc2)ISwApp.GetFirstDocument();
            while (modDoc != null)
            {
                if (!openDocs.Contains(modDoc))
                {
                    AttachModelDocEventHandler(modDoc);
                }
                modDoc = (ModelDoc2)modDoc.GetNext();
            }
        }

        public bool AttachModelDocEventHandler(ModelDoc2 modDoc)
        {
            if (modDoc == null)
                return false;

            DocumentEventHandler docHandler = null;

            if (!openDocs.Contains(modDoc))
            {
                switch (modDoc.GetType())
                {
                    case (int)swDocumentTypes_e.swDocPART:
                    {
                        docHandler = new PartEventHandler(modDoc, this);
                        break;
                    }
                    case (int)swDocumentTypes_e.swDocASSEMBLY:
                    {
                        docHandler = new AssemblyEventHandler(modDoc, this);
                        break;
                    }
                    case (int)swDocumentTypes_e.swDocDRAWING:
                    {
                        docHandler = new DrawingEventHandler(modDoc, this);
                        break;
                    }
                    default:
                    {
                        return false; //Unsupported document type
                    }
                }
                docHandler.AttachEventHandlers();
                openDocs.Add(modDoc, docHandler);
            }
            return true;
        }

        public bool DetachModelEventHandler(ModelDoc2 modDoc)
        {
            DocumentEventHandler docHandler;
            docHandler = (DocumentEventHandler)openDocs[modDoc];
            openDocs.Remove(modDoc);
            modDoc = null;
            docHandler = null;
            return true;
        }

        public bool DetachEventHandlers()
        {
            DetachSwEvents();

            //Close events on all currently open docs
            DocumentEventHandler docHandler;
            int numKeys = openDocs.Count;
            object[] keys = new object[numKeys];

            //Remove all document event handlers
            openDocs.Keys.CopyTo(keys, 0);
            foreach (ModelDoc2 key in keys)
            {
                docHandler = (DocumentEventHandler)openDocs[key];
                docHandler.DetachEventHandlers(); //This also removes the pair from the hash
                docHandler = null;
            }
            return true;
        }

        public int OnDocChange()
        {

            return 1;
        }

        public int OnDocLoad(string docTitle, string docPath)
        {
            return 0;
        }

        private int FileOpenPostNotify(string FileName)
        {
            AttachEventsToAllDocuments();
            return 0;
        }

        public int OnFileNew(object newDoc, int docType, string templateName)
        {
            AttachEventsToAllDocuments();
            return 0;
        }

        public int OnModelChange()
        {
            return 0;
        }
    }
}