using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using KlonsP.DataSets;
using KlonsLIB.Data;
using KlonsLIB.Forms;
using KlonsLIB.Misc;
using KlonsAdapters = KlonsP.DataSets.KlonsPDataSetTableAdapters;
using KlonsRepAdapters = KlonsP.DataSets.KlonsPRepDataSetTableAdapters;
using KlonsP.Forms;

namespace KlonsP.Classes
{
    public class KlonsData: KlonsDataModule, IDisposable
    {
        private static KlonsData _KlonsData = null;

        private DataSetHelper _klonsDataSetHelper = null;
        private DataSetHelper _klonsRepDataSetHelper = null;

        public string Version = "014";
        
        public string SettingsFileName = GetBasePath() + "\\Config\\SettingsP.xml";
        public string MasterListFileName = GetBasePath() + "\\Config\\MasterListP.xml";
        public string FolderForXMLReports = GetBasePath() + "\\XMLReports";
        
        public KlonsSettings Settings = new KlonsSettings();
        public MasterList MasterList { get; private set; }
        public MasterEntry CurrentDBTag { get; private set;}

        private KlonsParams _Params = null;
        public ReportHelperP ReportHelper { get; private set; }

        private string _currentUserName = "";
        public Form_Main MyMainForm { get { return Form_Main.MyInstance as Form_Main; } }

        public ErrorList ErrorInfoList { get; } = new ErrorList();

        public List<WeakReference> CollectedRefs = new List<WeakReference>();

        private KlonsData()
        {
            _KlonsData = this;
            CurrentDBTag = null;

            LoadSettings();
            LoadMasterList();

            ColorThemeHelper.MyToolStripRenderer.SetColorTheme(Settings.ColorTheme);

            if (Settings.MasterEntry.Name != "")
            {
                var me = MasterList.GetMasterEntryByName(Settings.MasterEntry.Name);
                if (me != null)
                {
                    Settings.MasterEntry.CopyFrom(me);
                }
            }

            _Params = new KlonsParams();
            
            ReportHelper = new ReportHelperP();

            _klonsDataSetHelper = new DataSetHelper(
                typeof(KlonsPDataSet) , 
                typeof(KlonsAdapters.TableAdapterManager) ,
                typeof(KlonsAdapters.QueriesTableAdapter),
                "ConnectionString1",
                KlonsP.Properties.Settings.Default);

            _dataSetHelpers["KlonsData"] = _klonsDataSetHelper;

            _klonsRepDataSetHelper = new DataSetHelper(
                typeof(KlonsPRepDataSet), 
                typeof(KlonsRepAdapters.TableAdapterManager),
                null,
                "ConnectionString1",
                KlonsP.Properties.Settings.Default);

            _dataSetHelpers["KlonsRep"] = _klonsRepDataSetHelper;
        }

        public bool ConnectTo(MasterEntry me, string username, string userpsw)
        {
            string filename;
            if (string.IsNullOrEmpty(me.Path))
            {
                filename = GetBaseDBPath();
            }
            else
            {
                filename = me.Path;
                filename = filename.Replace("@", GetBaseDBPath());
            }
            filename += "\\" + me.FileName;
            if (!File.Exists(filename))
            {
                throw new Exception("Nav faila: " + filename);
            }

            if (CurrentDBTag != null)
            {
                try
                {
                    Params.Save();
                    if (KlonsTableAdapterManager?.TUSERSTableAdapter?.Connection != null)
                        KlonsTableAdapterManager.TUSERSTableAdapter.Connection.StateChange -= Connection_StateChange;
                }
                catch(Exception) { }
            }

            _klonsDataSetHelper = new DataSetHelper(
                typeof(KlonsPDataSet), 
                typeof(KlonsAdapters.TableAdapterManager),
                typeof(KlonsAdapters.QueriesTableAdapter),
                "ConnectionString1",
                KlonsP.Properties.Settings.Default);

            _dataSetHelpers["KlonsData"] = _klonsDataSetHelper;
            
            _klonsRepDataSetHelper = new DataSetHelper(
                typeof(KlonsPRepDataSet), 
                typeof(KlonsRepAdapters.TableAdapterManager),
                null,
                "ConnectionString1",
                KlonsP.Properties.Settings.Default);
            
            _dataSetHelpers["KlonsRep"] = _klonsRepDataSetHelper;

            string s = MasterList.GetTemplateByName(me.ConnStr);
            if (string.IsNullOrEmpty(s))
            {
                /*
                s = @"Data Source=(LocalDB)\v11.0; " +
                    "AttachDbFilename={0}; " +
                    "Integrated Security=True; " +
                    "Connect Timeout=30; " +
                    "Workstation ID={1}";
                 */
                s = "character set=UTF8;" +
                    "data source=localhost;" +
                    @"initial catalog={0};" +
                    "user id=aivars;" +
                    "password=parole";
            }

            s = string.Format(s, filename, username);
            
            _currentUserName = username;

            _klonsDataSetHelper.ConnectTo(s);
            _klonsRepDataSetHelper.ConnectTo(s);

            KlonsTableAdapterManager.TUSERSTableAdapter.Connection.StateChange += Connection_StateChange;
            KlonsTableAdapterManager.TUSERSTableAdapter.Connection.Open();

            CurrentDBTag = new MasterEntry(me);
            return true;
        }

        private void Connection_StateChange(object sender, StateChangeEventArgs e)
        {
            //Debug.Print("Connection_StateChange to:" + e.CurrentState);
            if (IsDisposed) return;
            if (e.CurrentState == ConnectionState.Open)
            {
                if (KlonsQueriesTableAdapter != null)
                    KlonsQueriesTableAdapter.SP_SET_USERNAME(CurrentUserName);
            }
        }

        public new static KlonsData St
        {
            get
            {
                if (_KlonsData == null)
                {
                    _KlonsData = new KlonsData();
                    _KlonsDataModule = _KlonsData;
                }
                return _KlonsData;
            }
        }

        public static void ResetInstance()
        {
            if (_KlonsData == null)
            {
                _KlonsData.Dispose();
                _KlonsData = null;
            }
            _KlonsData = new KlonsData();
            _KlonsDataModule = _KlonsData;
        }

        public KlonsPDataSet DataSetKlons
        {
            get { return _klonsDataSetHelper.DataSet as KlonsPDataSet; }
        }
        public KlonsPRepDataSet DataSetKlonsRep
        {
            get { return _klonsRepDataSetHelper.DataSet as KlonsPRepDataSet; }
        }

        public KlonsAdapters.TableAdapterManager KlonsTableAdapterManager
        {
            get { return _klonsDataSetHelper.TableAdapterManager as KlonsAdapters.TableAdapterManager; }
        }
        public KlonsAdapters.QueriesTableAdapter KlonsQueriesTableAdapter
        {
            get { return _klonsDataSetHelper.QueriesTableAdapter as KlonsAdapters.QueriesTableAdapter; }
        }
        public KlonsRepAdapters.TableAdapterManager KlonsRepTableAdapterManager
        {
            get { return _klonsRepDataSetHelper.TableAdapterManager as KlonsRepAdapters.TableAdapterManager; }
        }

        public string CurrentUserName
        {
            get { return _currentUserName; }
        }

        public void SetCurrentUserName(string username)
        {
            _currentUserName = username;
        }

        public bool TestUserPassword(string username, string password)
        {
            KlonsPDataSet.TUSERSRow r = DataSetKlons.TUSERS.FindByNM(username);
            if (r == null) return false;
            if (string.IsNullOrEmpty(r.PSW)) return true;
            if (password == null) return false;
            return r.PSW == password;
        }
        public bool ChangeUserPassword(string username, string password)
        {
            KlonsPDataSet.TUSERSRow r = DataSetKlons.TUSERS.FindByNM(username);
            if (r == null) return false;
            r.PSW = password;
            KlonsTableAdapterManager.TUSERSTableAdapter.Update(r);
            return true;
        }
        public string GetUserGroup(string username)
        {
            KlonsPDataSet.TUSERSRow r = DataSetKlons.TUSERS.FindByNM(username);
            if (r == null) return null;
            if (string.IsNullOrEmpty(r.TP)) return null;
            return r.TP;
        }

        public KlonsParams Params
        {
            get { return _Params; }
        }

        public DbDataAdapter GetKlonsSqlDataAdapter(string tablename)
        {
            return _klonsDataSetHelper.GetSqlDataAdapter(tablename);
        }

        public object GetKlonsAdapter(string tablename)
        {
            return _klonsDataSetHelper.GetDataAdapter(tablename);
        }
        public object GetKlonsAdapter(DataTable table)
        {
            return _klonsDataSetHelper.GetDataAdapter(table.TableName);
        }

        public object GetKlonsRepAdapter(string tablename)
        {
            return _klonsRepDataSetHelper.GetDataAdapter(tablename);
        }
        public object GetKlonsRepAdapter(DataTable table)
        {
            return _klonsRepDataSetHelper.GetDataAdapter(table.TableName);
        }

        public void SetUpTableManagerForUsersTable()
        {
            _klonsDataSetHelper.CreateAdapterForTable("TUSERS");
            KlonsTableAdapterManager.TUSERSTableAdapter.Fill(DataSetKlons.TUSERS);
        }

        public void SetUpTableManager()
        {
            _klonsDataSetHelper.SetUpTableManager();
            _klonsDataSetHelper.SetClearBeforeFillForAll(false);
            _klonsRepDataSetHelper.SetClearBeforeFillForAll(true);

            //_klonsDataSetHelper.SetClearBeforeFillFor(true, "", "");
        }

        public void FillParamsForUser(string username)
        {
            Params.FillForUser(username);
        }

        public void FillBaseTables()
        {
           // _klonsDataSetHelper.FillTables("RATES", "DEPARTMENTS", "PERSONS",
           //     "HOLIDAYS", "TIMEPLAN_LIST");
        }

        public int FillKlonsTable(string tablename)
        {
            return _klonsDataSetHelper.FillTable(tablename);
        }

        public static string GetBasePath()
        {
            string s = Utils.GetMyFolder();
            if (KlonsSettings.DesignMode)
            {
                DirectoryInfo dir1 = new DirectoryInfo(s);
                s = dir1.Parent.Parent.FullName;
            }
            return s;
        }

        public string GetBaseDBPath()
        {
            return GetBasePath() + "\\" + Settings.BaseDBPathX;
        }

        public void CreateNewDB(string name, string descr)
        {
            MasterEntry me = new MasterEntry();
            me.Name = name;
            me.Descr = descr;
            me.ConnStr = Settings.BaseConnStr;
            string path = GetBaseDBPath();
            me.FileName = Utils.GetNextFile(path, "klonsP_", "fdb");
            me.FileName = Utils.GetFileNameFromURL(me.FileName);
            CreateNewDB(me);
        }
        public void CreateNewDB(MasterEntry me)
        {
            MasterList.ConnectionList.Add(me);
            string path = GetBaseDBPath();
            string fnmbase = path + "\\base\\klonsP00.fdb";
            string fnmnew = path + "\\" + me.FileName;
            if (!File.Exists(fnmbase))
            {
                throw new Exception("Fails [" + fnmbase + "] netika atrasts.");
            }
            File.Copy(fnmbase, fnmnew);
            return;
        }


        public void LoadSettings()
        {
            Settings = KlonsSettings.LoadSettings(SettingsFileName);
            KlonsLIB.MyData.Settings = Settings;
        }

        public void SaveSettings()
        {
            Settings.SaveSettings(SettingsFileName);
        }

        public void LoadMasterList()
        {
            MasterList = Classes.MasterList.LoadList(MasterListFileName);
        }

        public void SaveMasterList()
        {
            MasterList.SaveList(MasterListFileName);
        }


        private bool IsDisposed = false; // To detect redundant calls

        public void Dispose()
        {
            if(KlonsTableAdapterManager?.TUSERSTableAdapter?.Connection != null)
                KlonsTableAdapterManager.TUSERSTableAdapter.Connection.StateChange -= Connection_StateChange;
            _KlonsData = null;
            IsDisposed = true;
        }
    }

}
