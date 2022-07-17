using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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
        private NpgsqlConnection conn;
        private string sql;
        private NpgsqlCommand cmd;
        private DataTable dt;
        public Form1()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            constring = String.Format(
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
            conn = new NpgsqlConnection(constring);
            try
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
            }
        }
    }
}
