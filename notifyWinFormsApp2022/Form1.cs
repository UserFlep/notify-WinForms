﻿using Newtonsoft.Json;
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
        private NpgsqlConnection conn;
        public Form1()
        {
            InitializeComponent();
        }

        async private void btnStart_Click(object sender, EventArgs e)
        {
            this.btnStart.Enabled = false;
            this.txtBoxHostname.Enabled = false;
            this.txtBoxPort.Enabled = false;
            this.txtBoxUsername.Enabled = false;
            this.txtBoxPassword.Enabled = false;
            this.txtBoxDBName.Enabled = false;
            this.txtBoxTableName.Enabled = false;
            
            if (await this.CreateTable(this.txtBoxTableName.Text))
            {
                if (await this.InitTriggers(this.txtBoxTableName.Text))
                {
                    this.StartListening();
                    this.btnStop.Enabled = true;
                }
                else 
                {
                    this.lblStatus.ForeColor = Color.Red;
                    this.lblStatus.Text = "Ошибка инициализации триггеров";
                }
            }
            else
            {
                this.lblStatus.ForeColor = Color.Red;
                this.lblStatus.Text = "Ошибка подключения";
            }
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            this.StopListening();

            this.txtBoxHostname.Enabled = true;
            this.txtBoxPort.Enabled = true;
            this.txtBoxUsername.Enabled = true;
            this.txtBoxPassword.Enabled = true;
            this.txtBoxDBName.Enabled = true;
            this.txtBoxTableName.Enabled = true;
            this.btnStart.Enabled = true;
            this.btnStop.Enabled = false;
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.StopListening();
        }
        //Получение строки подклчения
        private string GetConnectionString(int keepalive = 1)
        {
            var connStringBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = this.txtBoxHostname.Text,
                Port = Convert.ToInt32(this.txtBoxPort.Text),
                Username = this.txtBoxUsername.Text,
                Password = this.txtBoxPassword.Text,
                Database = this.txtBoxDBName.Text,
                KeepAlive = keepalive
            };

            Debug.Print(connStringBuilder.ConnectionString);
            return connStringBuilder.ConnectionString;
        }
        //Запуск прослушивания
        async private void StartListening()
        {
            string connectionstring = string.Empty;

            try
            {
                connectionstring = this.GetConnectionString();
                this.conn = new NpgsqlConnection(connectionstring);
                await this.conn.OpenAsync();

                if (this.conn.State == ConnectionState.Open)
                {
                    this.lblStatus.ForeColor = Color.Green;
                    this.lblStatus.Text = "Подключено";

                    using (var command = new NpgsqlCommand("LISTEN mynotification", this.conn))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                    //Начальная инициализаци таблицы
                    using (var command = new NpgsqlCommand(@"SELECT * FROM " + this.txtBoxTableName.Text, this.conn))
                    {
                        var reader = await command.ExecuteReaderAsync();
                        var dt = new DataTable();
                        dt.Load(reader);

                        dgvData.DataSource = null; //очистка
                        dgvData.DataSource = dt;
                    }

                    this.conn.Notification += this.PostgresNotification;
                    this.btnStop.Enabled = true;
                }
                else
                {
                    this.lblStatus.ForeColor = Color.Red;
                    this.lblStatus.Text = "Не удалось подключиться";
                }
            }
            catch
            {
                MessageBox.Show("StartListening error: " + connectionstring);
            }
        }
        //Отклчение прослушивания
        async private void StopListening()
        {
            if (this.conn != null && this.conn.State != ConnectionState.Closed)
            {
                try
                {
                    this.conn.Notification -= this.PostgresNotification;

                    using (var command = new NpgsqlCommand("UNLISTEN mynotification", this.conn))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                    await this.conn.CloseAsync();
                }
                catch (Exception ex) {
                    MessageBox.Show("StopListening error " + ex);
                }
                
            }
            this.lblStatus.ForeColor = Color.Red;
            this.lblStatus.Text = "Отключено";
        }
        //Реакция на уведомления об изменениях
        private void PostgresNotification(object sender, NpgsqlNotificationEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, NpgsqlNotificationEventArgs>(this.PostgresNotification), new object[] { sender, e });
            }
            else
            {
                const string id = "id";
                const string flag = "flag";
                const string data = "data";
                //Десериализация оновленных данных типа:
                //{"operation" : "INSERT", "record" : {"id":121,"flag":false,"data":"Test data"}}
                var deserializedPgTgData = JsonConvert.DeserializeObject<PgTgData>(e.Payload);

                //Создание таблицы с обноленными данными
                var dt = new DataTable();

                //запись имеющихся в dataGridView данных в промежуточную таблицу
                dt = (DataTable)dgvData.DataSource;
                switch (deserializedPgTgData.operation)
                {
                    case "INSERT":
                        var newRow = dt.NewRow(); //Создание новой строки
                        newRow[id] = deserializedPgTgData.record.id;
                        newRow[flag] = deserializedPgTgData.record.flag;
                        newRow[data] = deserializedPgTgData.record.data;
                        dt.Rows.Add(newRow); //Добавление новой строки в таблицу
                        break;
                    case "UPDATE":
                        foreach (DataRow dr in dt.Rows)
                        {
                            if ((int)dr[id] == deserializedPgTgData.record.id) // if id==2
                            {
                                dr[flag] = deserializedPgTgData.record.flag;
                                dr[data] = deserializedPgTgData.record.data;
                                break;
                            }
                        }
                        break;
                    case "DELETE":
                        var removedRow = dt.Select("id=" + deserializedPgTgData.record_id).FirstOrDefault();
                        if (removedRow != null)
                        {
                            dt.Rows.Remove(removedRow);
                        }
                        break;
                }
                dgvData.DataSource = dt; //запись обновленных данных в dataGridView
            }
        }
        //Создание таблицы при ее отсутствии
       async private Task<bool> CreateTable(string tableName)
        {
            try
            {
                string connectionString = this.GetConnectionString();
                var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                if (conn.State != ConnectionState.Open)
                {
                    return false;
                }

                string sql = string.Format(@"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{0}'", tableName);

                if (await this.ExecuteScalar(connectionString, sql))
                {
                    // Таблица уже существует.
                    return true;
                }

                this.lblStatus.ForeColor = Color.Black;
                this.lblStatus.Text = "Создана таблица " + tableName;

                sql = @"CREATE TABLE XXXX
                (
                    id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
                    flag boolean NOT NULL DEFAULT false,
                    data text NOT NULL,
                    PRIMARY KEY (id)
                );";

                sql = sql.Replace("XXXX", tableName);

                using (var command = new NpgsqlCommand(sql, conn))
                {
                    await command.ExecuteNonQueryAsync();
                }

                await conn.CloseAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        //Выполнение запроса на изменение данных
        async private Task<bool> ExecuteScalar(string connectionString, string query)
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var command = conn.CreateCommand();
                command.CommandText = query;
                var result = command.ExecuteScalar();
                await conn.CloseAsync();
                if (result != null && result.ToString() != "0")
                {
                    return true;
                }

            }
            return false;
        }
        //Инициализация триггеров таблицы
        async private Task<bool> InitTriggers(string tablename)
        {
            this.lblStatus.ForeColor = Color.Black;
            this.lblStatus.Text = "Инициализаци тригеров";

            string connectionstring = this.GetConnectionString();
            var conn = new NpgsqlConnection(connectionstring);
            try
            {
                await conn.OpenAsync();
                var sb = new StringBuilder();
                sb.AppendLine(
                    @"CREATE OR REPLACE FUNCTION XXXX_update_notify() RETURNS trigger AS $$
                    DECLARE
                        id integer;
                    BEGIN
                        IF TG_OP = 'INSERT' OR TG_OP = 'UPDATE' THEN
                            id = NEW.id;
                        ELSE
                            id = OLD.id;
                        END IF;
                    PERFORM pg_notify(
                        'mynotification', 
                        json_build_object(
                            'operation', TG_OP,
                            'record_id', id,
                            'record', row_to_json(NEW)
                        )::text
                    );
                    RETURN NEW;
                    END;
                    $$ LANGUAGE plpgsql;"
                );
                // Удаление триггеров если они уже есть
                sb.AppendLine(@"DROP TRIGGER IF EXISTS XXXX_notify_update ON XXXX;");
                sb.AppendLine(@"CREATE TRIGGER XXXX_notify_update AFTER UPDATE ON XXXX FOR EACH ROW EXECUTE PROCEDURE XXXX_update_notify();");
                sb.AppendLine("DROP TRIGGER IF EXISTS XXXX_notify_insert ON XXXX;");
                sb.AppendLine("CREATE TRIGGER XXXX_notify_insert AFTER INSERT ON XXXX FOR EACH ROW EXECUTE PROCEDURE XXXX_update_notify();");
                sb.AppendLine("DROP TRIGGER IF EXISTS XXXX_notify_delete ON XXXX;");
                sb.AppendLine("CREATE TRIGGER XXXX_notify_delete AFTER DELETE ON XXXX FOR EACH ROW EXECUTE PROCEDURE XXXX_update_notify();");
                string sqlTrigger = sb.ToString().Replace("XXXX", tablename);

                using (var command = new NpgsqlCommand(sqlTrigger, conn))
                {
                    await command.ExecuteNonQueryAsync();
                }
                return true;

            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
                return false;
            }
            finally {
                await conn.CloseAsync();
            }
        }
    }

    ////Для десериализации тригеров//////////////////////////////////
    public class Record
    {
        public int id { get; set; }
        public bool flag { get; set; }
        public string data { get; set; }
    }

    public class PgTgData
    {
        public string operation { get; set; }
        public int record_id { get; set; }
        public Record record { get; set; }
    }
    /////////////////////////////////////////////////////////////////// 
}
