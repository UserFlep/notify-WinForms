using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace notifyWinFormsApp2022
{
    public partial class Form1 : Form
    {
        private string constring;
        private NpgsqlConnection notificationConnection;
        private string sql;
        private NpgsqlCommand cmd;
        private DataTable dt;
        public Form1()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            /*constring = String.Format(
                "Server={0};" +
                "Port={1};" +
                "User Id={2};" +
                "Password={3};" +
                "Database={4}",
                txtBoxHostname.Text, 
                txtBoxPort.Text, 
                txtBoxUsername.Text,
                txtBoxPassword.Text, 
                txtBoxDBName.Text
            );
            conn = new NpgsqlConnection(constring);*/
            /*try
            {
                conn.Open();
                //conn.Notification += (o, e) => Console.WriteLine("Received notification");
                
                sql = @"select * from " + txtBoxTableName.Text;
                cmd = new NpgsqlCommand(sql, conn);
                var reader = cmd.ExecuteReader();
                dt = new DataTable();
                dt.Load(reader);
                conn.Close();

                dgvData.DataSource = null; //очистка
                dgvData.DataSource = dt;
                //while (true)
                //{
                 //   conn.Wait();   // Thread will block here
                //}
            }
            catch (Exception ex)
            {
                conn.Close();
                MessageBox.Show("Error:" + ex);
            }
            finally
            {
                if(conn.State != ConnectionState.Closed)
                    conn.Close();
            }*/
            Debug.WriteLine(@"StartListening");
            StartListening();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            Debug.WriteLine(@"StopListening");
            StopListening();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Debug.WriteLine(@"StopListening");
            StopListening();
        }

        //////////////Прочие функции

        /// <summary>
        /// Fires PostgresNotification() on notification event.
        /// </summary>
        private void StartListening()
        {
            string connectionstring = String.Format(
                "Server={0};" +
                "Port={1};" +
                "User Id={2};" +
                "Password={3};" +
                "Database={4}",
                txtBoxHostname.Text,
                txtBoxPort.Text,
                txtBoxUsername.Text,
                txtBoxPassword.Text,
                txtBoxDBName.Text
            );
            notificationConnection = new NpgsqlConnection(connectionstring);

            try
            {
                notificationConnection.Open();

                if ( notificationConnection.State == ConnectionState.Open)
                {
                     lblStatus.ForeColor = Color.Green;
                     lblStatus.Text = @"Подключено";

                    using (var command = new NpgsqlCommand("listen mynotification",  notificationConnection))
                    {
                        command.ExecuteNonQuery();
                    }

                     notificationConnection.Notification +=  PostgresNotification;
                     btnStop.Enabled = true;
                }
                else
                {
                    lblStatus.ForeColor = Color.Red;
                    lblStatus.Text = @"Ошибка подключения !";
                }
            }
            catch
            {
                MessageBox.Show("Connection error: " + connectionstring);
            }
        }

        private void StopListening()
        {
            lblStatus.ForeColor = Color.Red;
            lblStatus.Text = @"Отключено";

            if ( notificationConnection != null &&  notificationConnection.State != ConnectionState.Closed)
            {
                 notificationConnection.Notification -=  PostgresNotification;

                using (var command = new NpgsqlCommand("unlisten mynotification",  notificationConnection))
                {
                    command.ExecuteNonQuery();
                }

                 notificationConnection.Close();
            }
        }

        

        /// <summary>
        /// Execute scalar command that can return a value.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="query">The query.</param>
        /// <returns>True if a result was returned.</returns>
        private bool ExecuteScalar(string connectionString, string query)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = query;
                var result = command.ExecuteScalar();

                if (result != null && result.ToString() != "0")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Initialize the triggers for UPDATE, INSERT and DELETE.
        /// They will call FUNCTION tablename_update_notify().
        /// </summary>
        /// <param name="tablename">The table name.</param>
       

        /// <summary>
        /// Postgres notification event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void PostgresNotification(object sender, NpgsqlNotificationEventArgs e)
        {
            if ( InvokeRequired)
            {
                 Invoke(new Action<object, NpgsqlNotificationEventArgs>( PostgresNotification), new object[] { sender, e });
            }
            else
            {
                Debug.WriteLine(@"Notification -->");
                Debug.WriteLine(@"  DATA {0}", e.Payload);
                Debug.WriteLine(@"  CHANNEL {0}", e.Channel);
                Debug.WriteLine(@"  PID {0}", e.PID);
            }
        }

        
    }
}
