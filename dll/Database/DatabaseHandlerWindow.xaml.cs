﻿using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ssi
{
    /// <summary>
    /// Interaktionslogik für DatabaseHandlerWindow.xaml
    /// </summary>
    public partial class DatabaseHandlerWindow : Window
    {
        private MongoClient mongo;
        private IMongoDatabase database;
        private string connectionstring = "mongodb://127.0.0.1:27017";
        private int authlevel = 0;
        private List<DatabaseMediaInfo> ci;
        private List<string> files = new List<string>();
        private List<string> allfiles = new List<string>();

        public DatabaseHandlerWindow()
        {
            InitializeComponent();

            this.db_server.Text = Properties.Settings.Default.MongoDBIP;
            this.db_login.Text = Properties.Settings.Default.MongoDBUser;
            this.db_pass.Password = Properties.Settings.Default.MongoDBPass;
            this.server_login.Text = Properties.Settings.Default.DataServerLogin;
            this.server_pass.Password = Properties.Settings.Default.DataServerPass;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MongoDBIP = this.db_server.Text;
            Properties.Settings.Default.MongoDBUser = this.db_login.Text;
            Properties.Settings.Default.MongoDBPass = this.db_pass.Password;
            Properties.Settings.Default.Save();

            connectionstring = "mongodb://" + Properties.Settings.Default.MongoDBUser + ":" + Properties.Settings.Default.MongoDBPass + "@" + Properties.Settings.Default.MongoDBIP;

            try
            {
                mongo = new MongoClient(connectionstring);

                authlevel = checkAuth(this.db_login.Text, "admin");

                if (authlevel > 0) SelectDatabase();
                else MessageBox.Show("You have no rights to access the database list");
            }
            catch { MessageBox.Show("Could not connect to Database!"); }

            authlevel = checkAuth(this.db_login.Text, Properties.Settings.Default.Database);
        }

        private void DataBasResultsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataBasResultsBox.SelectedItem != null)
            {
                Properties.Settings.Default.Database = DataBasResultsBox.SelectedItem.ToString();
                Properties.Settings.Default.Save();

                GetSessions();
            }
        }

        private void CollectionResultsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CollectionResultsBox.SelectedItem != null)
            {
                Properties.Settings.Default.LastSessionId = ((DatabaseSession)(CollectionResultsBox.SelectedValue)).Name;
                Properties.Settings.Default.Save();

                GetAnnotations();
                GetMedia();
            }
        }

        private void GetMedia()
        {
            MediaResultBox.Items.Clear();
            ci = GetMediaFromDB(Properties.Settings.Default.Database, Properties.Settings.Default.LastSessionId);

            foreach (DatabaseMediaInfo c in ci)
            {
                files.Add(c.filepath);
                if (!c.filepath.Contains(".stream~"))
                {
                    MediaResultBox.Items.Add(c.filename);
                }
            }
        }

        private void requireslogin_Checked(object sender, RoutedEventArgs e)
        {
            server_login.IsEnabled = true;
            server_pass.IsEnabled = true;
        }

        private void requireslogin_Unchecked(object sender, RoutedEventArgs e)
        {
            server_login.IsEnabled = false;
            server_pass.IsEnabled = false;
        }

        private void showonlymine_Checked(object sender, RoutedEventArgs e)
        {
            GetAnnotations(true);
        }

        private void showonlymine_Unchecked(object sender, RoutedEventArgs e)
        {
            GetAnnotations(false);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DataServerLogin = this.server_login.Text;
            Properties.Settings.Default.DataServerPass = this.server_pass.Password;
            Properties.Settings.Default.Save();

            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        public System.Collections.IList Annotations()
        {
            if (AnnotationResultBox.SelectedItems != null)
                return AnnotationResultBox.SelectedItems;
            else return null;
        }

        public List<DatabaseMediaInfo> MediaConnectionInfo()
        {
            if (ci != null)
                return ci;
            else return null;
        }

        public List<string> Media()
        {
            if (MediaResultBox.SelectedItems != null)
                return allfiles;
            else return null;
        }

        private int checkAuth(string dbuser, string db = "admin")
        {
            //4 = root
            //3 = admin
            //2 = write
            //1 = read
            //0 = notauthorized

            int auth = 0;
            try
            {
                var adminDB = mongo.GetDatabase(db);
                var cmd = new BsonDocument("usersInfo", dbuser);
                var queryResult = adminDB.RunCommand<BsonDocument>(cmd);
                var roles = (BsonArray)queryResult[0][0]["roles"];

                for (int i = 0; i < roles.Count; i++)
                {
                    if (roles[i]["role"].ToString() == "root" || roles[i]["role"].ToString() == "dbOwner" && auth < 4) auth = 4;
                    else if (roles[i]["role"].ToString() == "userAdminAnyDatabase" || roles[i]["role"].ToString() == "userAdmin" && auth < 3) auth = 3;
                    else if (roles[i]["role"].ToString() == "readWriteAnyDatabase" || roles[i]["role"].ToString() == "readWrite" && auth < 2) auth = 2;
                    else if (roles[i]["role"].ToString() == "readAnyDatabase" || roles[i]["role"].ToString() == "read" && auth < 1) auth = 1;
                    else auth = 0;

                    //edit/add more roles if you want to change security levels
                }
            }
            catch
            {
                var adminDB = mongo.GetDatabase("admin");
                var cmd = new BsonDocument("usersInfo", dbuser);
                var queryResult = adminDB.RunCommand<BsonDocument>(cmd);
                var roles = (BsonArray)queryResult[0][0]["roles"];

                for (int i = 0; i < roles.Count; i++)
                {
                    if (roles[i]["role"].ToString() == "root" || roles[i]["role"].ToString() == "dbOwner" && auth < 4) auth = 4;
                    else if (roles[i]["role"].ToString() == "userAdminAnyDatabase" && auth < 3) auth = 3;
                    else if (roles[i]["role"].ToString() == "readWriteAnyDatabase" && auth < 2) auth = 2;
                    else if (roles[i]["role"].ToString() == "readAnyDatabase" && auth < 1) auth = 1;
                    else auth = 0;

                    //edit/add more roles if you want to change security levels
                }
            }

            return auth;
        }

        public void SelectDatabase()
        {
            DataBasResultsBox.Items.Clear();

            var databases = mongo.ListDatabasesAsync().Result.ToListAsync().Result;
            foreach (var c in databases)
            {
                if (c.GetElement(0).Value.ToString() != "admin" && c.GetElement(0).Value.ToString() != "local")
                    DataBasResultsBox.Items.Add(c.GetElement(0).Value.ToString());
            }
        }

        public void GetSessions()

        {
            database = mongo.GetDatabase(Properties.Settings.Default.Database);

            var sessioncollection = database.GetCollection<BsonDocument>("Sessions");
            var sessions = sessioncollection.Find(_ => true).ToList();

            if (CollectionResultsBox.Items != null) CollectionResultsBox.Items.Clear();
            List<DatabaseSession> items = new List<DatabaseSession>();
            foreach (var c in sessions)
            {
                //CollectionResultsBox.Items.Add(c.GetElement(1).Value.ToString());
                items.Add(new DatabaseSession() { Name = c["name"].ToString(), Location = c["location"].ToString(), Language = c["language"].ToString(), Date = c["date"].AsDateTime.ToShortDateString() });
            }

            CollectionResultsBox.ItemsSource = items;
        }

        public void GetAnnotations(bool onlyme = false)

        {
            AnnotationResultBox.ItemsSource = null;
            //  AnnotationResultBox.Items.Clear();
            List<DatabaseAnno> items = new List<DatabaseAnno>();
            List<string> Collections = new List<string>();

            database = mongo.GetDatabase(Properties.Settings.Default.Database);
            var sessions = database.GetCollection<BsonDocument>("Sessions");
            var annotations = database.GetCollection<BsonDocument>("Annotations");

            BsonDocument documents;
            var builder = Builders<BsonDocument>.Filter;
            if (onlyme)
            {
                var filter = builder.Eq("name", Properties.Settings.Default.LastSessionId);
                documents = sessions.Find(filter).Single();
            }
            else
            {
                var filter = builder.Eq("name", Properties.Settings.Default.LastSessionId);
                documents = sessions.Find(filter).Single();
            }

            foreach (var c in documents["annotations"].AsBsonArray)
            {
                var filteranno = builder.Eq("_id", c["annotation_id"].AsObjectId);
                var annos = annotations.Find(filteranno).Single();

                BsonElement value;
                if (annos.TryGetElement("annotator", out value) && annos.TryGetElement("annotator", out value))
                {
                    var filtera = builder.Eq("_id", annos["role_id"]);
                    var roledb =  database.GetCollection<BsonDocument>("Roles").Find(filtera).Single();
                    string roleid = roleid = roledb.GetValue(1).ToString();


                    var filterb = builder.Eq("_id", annos["scheme_id"]);
                    var annotdb = database.GetCollection<BsonDocument>("AnnotationSchemes").Find(filterb).Single();
                    string annotid = "unknown";
                    annotid = annotdb.GetValue(1).ToString();

                    // AnnotationResultBox.Items.Add(roleid + "#" + annotid + "#" + a["annotator"].ToString());
                    //  AnnotationResultBox.Items.Add(roleid);

                    if (onlyme)
                    {
                        if (Properties.Settings.Default.MongoDBUser == annos["annotator"].ToString())
                        {
                            items.Add(new DatabaseAnno() { Role = roleid, AnnoType = annotid, Annotator = annos["annotator"].ToString() });
                        }
                    }
                    else items.Add(new DatabaseAnno() { Role = roleid, AnnoType = annotid, Annotator = annos["annotator"].ToString() });
                }
            }
            AnnotationResultBox.ItemsSource = items;
        }

        public List<DatabaseMediaInfo> GetMediaFromDB(string db, string session)
        {
            BsonElement value;
            List<DatabaseMediaInfo> paths = new List<DatabaseMediaInfo>();
            var colllection = database.GetCollection<BsonDocument>("Sessions");
            var media = database.GetCollection<BsonDocument>("Media");

            var builder = Builders<BsonDocument>.Filter;
            var filter = builder.Eq("name", session);

            var documents = colllection.Find(filter).ToList();

            foreach (var document in documents)
            {
                string id;
                if (document.TryGetElement("name", out value)) id = document["name"].ToString();
                if (document.TryGetElement("media", out value))
                {
                    ObjectId media_id;
                    BsonArray files = document["media"].AsBsonArray;

                    for (int i = 0; i < files.Count; i++)
                    {
                        media_id = files[i]["media_id"].AsObjectId;

                        var filtermedia = builder.Eq("_id", media_id);
                        var selectedmedialist = media.Find(filtermedia).ToList();

                        if (selectedmedialist.Count > 0)
                        {
                            var selectedmedia = selectedmedialist[0];
                            DatabaseMediaInfo c = new DatabaseMediaInfo();
                            c.connection = selectedmedia["connection"].ToString();
                            c.ip = selectedmedia["ip"].ToString();
                            c.folder = selectedmedia["folder"].ToString();
                            c.filepath = selectedmedia["filePath"].ToString();
                            c.filename = selectedmedia["fileName"].ToString();
                            c.requiresauth = selectedmedia["requiresAuth"].ToString();

                            //Todo: solve references
                            c.subject = selectedmedia["subject_id"].ToString();
                            c.role = selectedmedia["role_id"].ToString();
                            c.mediatype = selectedmedia["mediatype_id"].ToString();

                            paths.Add(c);
                        }
                    }
                }
            }
            return paths;
        }

        private void MediaResultBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            allfiles.Clear();
            if (MediaResultBox.SelectedItem != null)
            {
                for (int i = 0; i < MediaResultBox.SelectedItems.Count; i++)
                {
                    for (int j = 0; j < files.Count; j++)
                    {
                        if (files[j].Contains(MediaResultBox.SelectedItems[i].ToString()))
                        {
                            allfiles.Add(files[j].ToString());
                        }
                    }
                }

                foreach (DatabaseMediaInfo c in ci)
                {
                    if (c.filename == MediaResultBox.SelectedItem.ToString())
                    {
                        if (c.requiresauth == "true")
                        {
                            requireslogin.IsChecked = true;
                            requireslogin.IsEnabled = true;
                        }
                        else
                        {
                            requireslogin.IsEnabled = false;
                            requireslogin.IsChecked = false;
                        }
                    }
                }
            }
        }

        private void DeleteAnnotation_Click(object sender, RoutedEventArgs e)
        {
            if (AnnotationResultBox.SelectedItem != null)
            {
                var sessions = database.GetCollection<BsonDocument>("Sessions");
                var roles = database.GetCollection<BsonDocument>("Roles");
                var AnnotationSchemes = database.GetCollection<BsonDocument>("AnnotationSchemes");
                var annotations = database.GetCollection<BsonDocument>("Annotations");

                ObjectId roleid = new ObjectId();
                var builder = Builders<BsonDocument>.Filter;
                var filtera = builder.Eq("name", ((DatabaseAnno)(AnnotationResultBox.SelectedValue)).Role);
                var roledb = roles.Find(filtera).ToList();
                if (roledb.Count > 0) roleid = roledb[0].GetValue(0).AsObjectId;

                ObjectId annotid = new ObjectId(); ;
                var filterb = builder.Eq("name", ((DatabaseAnno)(AnnotationResultBox.SelectedValue)).AnnoType);
                var annotdb = AnnotationSchemes.Find(filterb).ToList();
                if (annotdb.Count > 0) annotid = annotdb[0].GetValue(0).AsObjectId;

                var filter = builder.Eq("role_id", roleid) & builder.Eq("scheme_id", annotid) & builder.Eq("annotator", ((DatabaseAnno)(AnnotationResultBox.SelectedValue)).Annotator);
                var anno = annotations.Find(filter).ToList();

                var filter2 = builder.Eq("name", ((DatabaseSession)(CollectionResultsBox.SelectedValue)).Name);
                var session = sessions.Find(filter2).ToList();
                if (session.Count > 0)

                {
                    var annos = session[0]["annotations"].AsBsonArray;

                    for (int i = 0; i < annos.Count; i++)
                    {
                        if (annos[i]["annotation_id"] == anno[0]["_id"])
                        {
                            annos.RemoveAt(i);
                            break;
                        }
                    }

                    var update = Builders<BsonDocument>.Update.Set("annotations", annos);
                    sessions.UpdateOne(filter2, update);
                }

                var result = annotations.DeleteOne(filter);
                GetAnnotations();
            }
        }

        private void AnnotationResultBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AnnotationResultBox.SelectedValue != null)
            {
                for (int i = 0; i < AnnotationResultBox.SelectedItems.Count; i++)
                {
                    if (authlevel > 2 || Properties.Settings.Default.MongoDBUser == ((DatabaseAnno)(AnnotationResultBox.SelectedValue)).Annotator) DeleteAnnotation.Visibility = Visibility.Visible;
                }
            }
        }
    }
}