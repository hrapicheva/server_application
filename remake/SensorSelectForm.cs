using Designator_vp_dev.DatabaseContext;
using System.Data;
using vp_dev.DatabaseContext;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Immutable;
using Npgsql;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Text;
using Microsoft.VisualBasic.ApplicationServices;
using static Server_App_3._3.SensorSelectForm;

namespace Server_App_3._3
{
    public partial class SensorSelectForm : Form
    {
        public struct DBShemaInsert
        {
            public List<long> sensor_cid;
            public List<int> serialList;
            //таблица номеров изделий
            public string sensor;
            //Для выбранных teds_data.sensor_oid сбор данных по...
            public string teds_data;
            public string type_cal; //...type_cal_oid в соответствии с sensor_oid из таблицы type_cal
            public string teds_template; //teds_template_oid в соответствии с sensor_oid из таблицы teds_template
            public string default_teds_parametrs; //default_teds_parametrs_oid в соответствии с sensor_oid из таблицы default_teds_parametrs
            public string teds_employee; //author_oid в соответствии с sensor_oid из таблицы TedsEmployee
            public string organization;//manufacturer_oid в соответствии с sensor_oid из таблицы Organization
        }

        DBShemaInsert db_insert;

        public SensorSelectForm(string EmployeeFullName)
        {
            InitializeComponent();
            buttonPrint.Enabled = false;
            // Назначить обработчики события TextChanged для TextBoxInputSerial
            textBoxInputSerial.TextChanged += TextBox_TextChanged;
            //вывод фио пользователя
            lableDisplayName.Text = EmployeeFullName;
            labelFindSerial.Text = "Введите номера изделий и выполните поиск по базе.";
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку данных из таблицы Sensors на основе значений serial
                var sensor_data = (dbContext.Sensors.Select(sensor => sensor.Serial)).ToList();
                sensor_data.Sort();
                labelFindSerial.Text += "\n\nМинимальный номер датчика: " + sensor_data.First();
                labelFindSerial.Text += "\nМаксимальный номер датчика: " + sensor_data.Last();
            }

            //УДАЛИТЬ
            textBoxInputSerial.Text = "222587,222591-222594,5647576,220154";
        }

        //смена пользователя
        private void buttonChangeUser_Click(object sender, EventArgs e)
        {
            this.Hide();
            LoginForm lf = new LoginForm();
            lf.Show();
        }

        //состояние кнопок
        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            // Проверить состояние полей ввода и включить/отключить кнопку
            buttonPrint.Enabled = false;
            if (!string.IsNullOrWhiteSpace(textBoxInputSerial.Text))
            {
                buttonFind.Enabled = true;
            }
            else
            {
                buttonFind.Enabled = false;
            }
        }

        //поиск по номерам изделий
        private async void buttonFind_Click(object sender, EventArgs e)
        {

            buttonFind.Enabled = false;
            string fileSensorSerial = "C:\\Users\\aakhrapicheva\\source\\repos\\Server_App_3.3\\FindResult\\find_result.txt";
            string serials_str = string.Empty;
            string UserMessageSerialFinding = string.Empty;
            labelError.Text = string.Empty;

            try
            {
                string SerialSelection = textBoxInputSerial.Text;
                //преобразование данных номеров и интервалов номеров в список
                List<int> serialList = ParseIndexSelection(SerialSelection).ToList();

                //добавление данных в глобальную переменную
                db_insert.serialList = serialList;

                labelFindSerial.Text = "Выполняется поиск по базе...";
                Application.DoEvents(); // Обновление интерфейса

                //поиск номеров по базе 
                using (var dbContext = new VpDevaContext())
                {
                    //запрос к serials на совпадение с данными массива
                    // Выполнение запроса к базе данных в отдельном потоке
                    var query = await Task.Run(() =>
                    {
                        using (var dbContext = new VpDevaContext())
                        {
                            //запрос к serials на совпадение с данными массива
                            return dbContext.Sensors
                                .Where(sensor => serialList.Contains((int)sensor.Serial))
                                .Select(sensor => sensor.Serial)
                                .ToList();
                        }
                    });
                    string serialInfo = string.Empty;

                    if (query.Count == 0)
                    {
                        labelFindSerial.Text = "Поиск завершен.\nСерийные номера не найдены.";
                        buttonPrint.Enabled = false;
                    }
                    else
                    {
                        
                        labelFindSerial.Text = "Поиск завершен.\nСерийные номера найдены.";
                        buttonPrint.Enabled = true;
                        
//----------------------------потом убрать-------------------------------------------//
                        // Получаем список строк, представляющих значения из списка query
                        List<string> serial_list_str = query.Select(serial => serial.ToString()).ToList();
                        // Преобразуем список строк в одну строку, разделенную запятыми
                        serials_str = string.Join(", ", serial_list_str);
                    }
                    // Записываем результат в файл
                    File.WriteAllText(fileSensorSerial, serials_str);
                }
            }
            catch (ArgumentException ex)
            {
                labelError.Text = ex.Message;
                labelFindSerial.Text = string.Empty;
            }
            buttonFind.Enabled = true;
        }


        // поиск номеров по базе
        // совпадение - флаг совпадения, не нашло - флаг
        public int[] ParseIndexSelection(string SerialSelection)
        {
            List<int> DuplicateSerialNumbers = new List<int>();

            // Разделение строки по запятым для получения отдельных элементов
            string[] parts = SerialSelection.Split(',');

            foreach (string part in parts)
            {
                string trimmedPart = part.Trim();

                if (trimmedPart.Contains("-"))
                {
                    // Если элемент содержит диапазон изделий
                    string[] range = trimmedPart.Split('-');

                    if (range.Length == 2 && int.TryParse(range[0], out int start) && int.TryParse(range[1], out int end) && start <= end)
                    {
                        // Проверка правильности формата интервала изделий и добавление всех номеров в указанном диапазоне
                        for (int i = start; i <= end; i++)
                        {
                            DuplicateSerialNumbers.Add(i);
                        }
                    }
                    else
                    {
                        throw new ArgumentException("Некорректный формат интервала номеров изделий.");
                    }
                }
                else if (int.TryParse(trimmedPart, out int SerialNumber))
                {
                    // Если элемент является отдельным номером изделий
                    DuplicateSerialNumbers.Add(SerialNumber);
                }
                else
                {
                    throw new ArgumentException("Некорректный формат номера изделия.");
                }
            }

            // Сортировка номеров изделий по возрастаниюи удаление дубликатов
            DuplicateSerialNumbers.Sort();
            int[] SerialNumbers = DuplicateSerialNumbers.Distinct().ToArray();

            return SerialNumbers.ToArray();
        }

        private void buttonPrint_Click(object sender, EventArgs e)
        {
            labelInfoDB.Text = string.Empty;
            //данные из таблиц храним в структуре
            //сформировать скрипт
            ScriptInsert();
            InsertUserDB();
            labelInfoDB.Text = "Сбор данных завершен!";
            //при вставке смотрим существуют ли данные с таким заводским номером и датой и исходя из этого пропускаем/вставляем данные
            //проверка версии базы данных
        }
        public DBShemaInsert ScriptInsert()
        {
            labelInfoDB.Text = "Выполняется сбор данных...";
            Application.DoEvents(); // Обновление интерфейса
            using (var dbContext = new VpDevaContext())
            {
                //запрос запрос в базу нужные cid
                db_insert.sensor_cid = dbContext.Sensors
                .Where(sensor => db_insert.serialList.Contains((int)sensor.Serial))
                .Select(sensor => sensor.Cid)
                .ToList();

                // Записываем JSON-строку таблицы Sensor в файл и переменную
                db_insert.sensor = SerialScript(); //json sensor
                //Сбор данных по teds_data.sensor_oid == sensor.cid в таблице teds_data
                db_insert.teds_data = TedsScript(); //json teds

                // Записываем данные таблиц, связанных с teds_data в структуру db_shema_json
                //Для выбранных teds_data.sensor_oid сбор данных по
                //type_cal_oid в соответствии с sensor_oid из таблицы type_call
                //teds_template_oid в соответствии с sensor_oid из таблицы teds_template
                //author_oid в соответствии с sensor_oid из таблицы author
                //manufacturer_oid в соответствии с sensor_oid из таблицы manufacturer
                //default_teds_parametrs_oid в соответствии с sensor_oid из таблицы default_teds_parametrs
                db_insert.default_teds_parametrs = DefaultTedsParametrsScript();
                db_insert.teds_employee = TedsEmployeeScript();
                db_insert.teds_template = TedsTemplateScript();
                db_insert.type_cal = TypeCalScript();
                db_insert.organization = OrganizationScript();
            }
            return db_insert;
        }
        public List<TedsDatum> TedsData()
        {
            using (var dbContext = new VpDevaContext())
            {
                var teds_data = dbContext.TedsData
                .Where(teds => db_insert.sensor_cid.Contains((long)teds.SensorOid))
                .ToList();
                return teds_data;
            }
        }
        public string SerialScript()
        {
            string tableName = "Sensor";

            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку данных из таблицы Sensors на основе значений serial
                var sensor_data = dbContext.Sensors
                    .Where(sensor => db_insert.serialList.Contains((int)sensor.Serial))
                    .Select(sensor => new
                    {
                        sensor.Cid,
                        sensor.Uuid,
                        sensor.Serial,
                        sensor.VariantSensorOid,
                        sensor.AuthorOid
                    })
                    .ToList();

                StringBuilder serial_script = new StringBuilder();

                // Добавляем строку с заголовками столбцов
                serial_script.AppendLine($"INSERT INTO vibropribor.sensor (cid, uuid, serial, variant_sensor_oid, author_oid) VALUES");

                // Добавляем строки вставки данных для каждого элемента в sensor_data
                foreach (var data in sensor_data)
                {
                    serial_script.AppendLine($"({data.Cid}, '{data.Uuid}', {data.Serial}, {data.VariantSensorOid}, {data.AuthorOid}),");
                }

                // Удаляем последнюю запятую в последней строке вставки данных
                serial_script.Length -= 3;
                serial_script.AppendLine(";");
                string script_insert_serial = serial_script.ToString();

                // Сериализуем данные в JSON
                //string json_serial = JsonConvert.SerializeObject(sensor_data, Formatting.Indented);
                // Создаем строку для хранения скрипта вставки данных в формате JSON
                //string insertScriptSeral = $"INSERT INTO {tableName} (column_name) VALUES ('{json_serial}')";

                // Записываем JSON-строку в файл
                File.WriteAllText("C:\\Users\\aakhrapicheva\\source\\repos\\Server_App_3.3\\FindResult\\script_insert_serial.txt", script_insert_serial);
                return script_insert_serial;
            }
        }
        public string TedsScript()
        {
            //string tableName = "teds_data";
            string filePathInsertSensor = "C:\\Users\\aakhrapicheva\\source\\repos\\Server_App_3.3\\Json\\script_insert_teds_data.sql";
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку данных из таблицы teds_data на основе значений serial
                var teds_data = dbContext.TedsData
                .Where(teds => db_insert.sensor_cid.Contains((long)teds.SensorOid))
                .Select(teds => new
                {
                    teds.Cid,
                    teds.Uuid,
                    teds.Date,
                    teds.TypeCalOid,
                    teds.TedsTemplateOid,
                    teds.SensorOid,
                    teds.AuthorOid,
                    teds.TedsData,
                    teds.ManufacturerOid,
                    teds.DefaultTedsParametrsOid
                })
                .ToList();

                StringBuilder teds_datainsert_script = new StringBuilder();

                // Добавляем строку с заголовками столбцов
                teds_datainsert_script.AppendLine($"INSERT INTO teds_data (cid, date, type_cal_oid, teds_template_oid, sensor_oid, author_oid, teds_data, manufacturer_oid, default_teds_parametrs_oid) VALUES");

                // Добавляем строки вставки данных для каждого элемента в teds_data
                foreach (var data in teds_data)
                {
                    teds_datainsert_script.AppendLine($"({data.Cid}, '{data.Date}', {data.TypeCalOid}, {data.TedsTemplateOid}, {data.SensorOid}, {data.AuthorOid}, '{data.TedsData}', {data.ManufacturerOid}, {data.DefaultTedsParametrsOid}),");
                }

                // Удаляем последнюю запятую в последней строке вставки данных
                teds_datainsert_script.Length -= 3;
                // Добавляем символ ";" в конец строки
                teds_datainsert_script.AppendLine(";");

                string script_insert_teds_data = teds_datainsert_script.ToString();

                File.WriteAllText(filePathInsertSensor, script_insert_teds_data);
                return script_insert_teds_data;
            }
        }
        public string DefaultTedsParametrsScript()
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData();

                // Получаем список связанных идентификаторов из таблицы default_teds_parametrs
                var default_teds_parametrs = dbContext.DefaultTedsParametrs
                .Where(d => teds_data.Select(t => t.DefaultTedsParametrsOid).Contains(d.Cid))
                .Select(d => new
                {
                    d.Cid,
                    d.TedsParametrs,
                    d.SensorType,
                    d.Uuid
                })
                .ToList();

                StringBuilder default_teds_parametrs_script = new StringBuilder();

                // Добавляем строку с заголовками столбцов
                default_teds_parametrs_script.AppendLine($"INSERT INTO vpref.default_teds_parametrs (cid, teds_parametrs, sensor_type, uuid) VALUES");

                // Добавляем строки вставки данных для каждого элемента в default_teds_parametrs
                foreach (var data in default_teds_parametrs)
                {
                    default_teds_parametrs_script.AppendLine($"({data.Cid}, '{data.TedsParametrs}', '{data.SensorType}', '{data.Uuid}'),");
                }

                // Удаляем последнюю запятую в последней строке вставки данных
                default_teds_parametrs_script.Length -= 3;

                // Добавляем символ ";" в конец строки
                default_teds_parametrs_script.AppendLine(";");

                string script_insert_default_teds_parametrs = default_teds_parametrs_script.ToString();

                //var json_default_teds_parametrs = JsonConvert.SerializeObject(default_teds_parametrs, Formatting.Indented);

                return script_insert_default_teds_parametrs;
            }
        }
        public string TedsEmployeeScript()
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData();

                // Получаем список связанных идентификаторов из таблицы teds.teds_employee
                var teds_employee = dbContext.TedsEmployees
                .Where(employee => teds_data.Select(t => t.AuthorOid).Contains(employee.Cid))
                .Select(employee => new
                {
                    employee.Cid,
                    employee.Uuid,
                    employee.EmployeeOid,
                    employee.PermissionOid,
                    employee.ViewTeds,
                })
                .ToList();
                StringBuilder teds_employee_script = new StringBuilder();

                // Добавляем строку с заголовками столбцов
                teds_employee_script.AppendLine($"INSERT INTO teds.teds_employee (teds_employee_script, teds_employee_script, teds_employee_script, teds_employee_script, teds_employee_script) VALUES");

                // Добавляем строки вставки данных для каждого элемента в teds_employee
                foreach (var data in teds_employee)
                {
                    teds_employee_script.AppendLine($"({data.Cid}, '{data.Uuid}', '{data.EmployeeOid}', '{data.PermissionOid}', '{data.ViewTeds}'),");
                }

                // Удаляем последнюю запятую в последней строке вставки данных
                teds_employee_script.Length -= 3;

                // Добавляем символ ";" в конец строки
                teds_employee_script.AppendLine(";");

                string script_insert_teds_employee = teds_employee_script.ToString();


                //var json_teds_employee = JsonConvert.SerializeObject(teds_employee, Formatting.Indented);

                return script_insert_teds_employee;
            }

        }
        public string TedsTemplateScript()
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData();

                // Получаем список связанных идентификаторов из таблицы teds_employee
                var teds_template = dbContext.TedsTemplates
                .Where(template => teds_data.Select(t => t.TedsTemplateOid).Contains(template.Cid))
                .Select(template => new
                {
                    template.Cid,
                    template.IdTemplate,
                    template.NameTemplate,
                    template.Description,
                    template.TedsParametrs,
                })
                .ToList();

                StringBuilder teds_template_script = new StringBuilder();

                // Добавляем строку с заголовками столбцов
                teds_template_script.AppendLine($"INSERT INTO teds.teds_template (cid, id_template, id_template, description, teds_parametrs) VALUES");

                // Добавляем строки вставки данных для каждого элемента в teds.teds_template
                foreach (var data in teds_template)
                {
                    teds_template_script.AppendLine($"({data.Cid}, '{data.IdTemplate}', '{data.NameTemplate}', '{data.Description}', '{data.TedsParametrs}'),");
                }

                // Удаляем последнюю запятую в последней строке вставки данных
                teds_template_script.Length -= 3;

                // Добавляем символ ";" в конец строки
                teds_template_script.AppendLine(";");

                string script_insert_teds_template = teds_template_script.ToString();
                //var json_teds_template = JsonConvert.SerializeObject(teds_template, Formatting.Indented);

                return script_insert_teds_template;
            }
        }
        public string TypeCalScript()
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData();

                // Получаем список связанных идентификаторов из таблицы vpref.type_cal
                var type_cal = dbContext.TypeCals
                .Where(c => teds_data.Select(t => t.TypeCalOid).Contains(c.Cid))
                .Select(c => new
                {
                    c.Cid,
                    c.Uuid,
                    c.DisplayName,
                    c.Description,
                    c.TedsCode,
                })
                .ToList();

                StringBuilder type_cal_script = new StringBuilder();

                // Добавляем строку с заголовками столбцов
                type_cal_script.AppendLine($"INSERT INTO vpref.type_cal (cid, uuid, display_name, description, teds_code) VALUES");

                // Добавляем строки вставки данных для каждого элемента в type_cal
                foreach (var data in type_cal)
                {
                    type_cal_script.AppendLine($"({data.Cid}, '{data.Uuid}', '{data.DisplayName}', '{data.Description}', '{data.TedsCode}'),");
                }

                // Удаляем последнюю запятую в последней строке вставки данных
                type_cal_script.Length -= 3;

                // Добавляем символ ";" в конец строки
                type_cal_script.AppendLine(";");

                string script_insert_type_cal = type_cal_script.ToString();
                //var json_type_cal = JsonConvert.SerializeObject(type_cal, Formatting.Indented);

                return script_insert_type_cal;
            }
        }
        public string OrganizationScript()
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData();

                // Получаем список связанных идентификаторов из таблицы vpref.organization
                var organization = dbContext.Organizations
                .Where(o => teds_data.Select(t => t.ManufacturerOid).Contains(o.Cid))
                .Select(o => new
                {
                    o.Cid,
                    o.Uuid,
                    o.Name,
                    o.ShortName,
                    o.LegalAddress,
                    o.ActualAddress,
                    o.PostalAddress,
                    o.Inn,
                    o.Kpp,
                    o.Bik,
                    o.Ogrn,
                    o.Okpo,
                    o.NcageOid,
                    o.IuOrganizationCodeOid,
                    o.DecimalOid,
                    o.Ieee1451ManufacturerOid
                })
                .ToList();


                StringBuilder organization_script = new StringBuilder();

                // Добавляем строку с заголовками столбцов
                organization_script.AppendLine($"INSERT INTO vpref.organization (cid, uuid, name, short_name, legal_address, actual_address, postal_address, inn, kpp, bik, ogrn, okpo, ncage_oid, iu_organization_code_oid, decimal_oid, ieee_1451_manufacturer_oid) VALUES");

                // Добавляем строки вставки данных для каждого элемента в organization
                foreach (var data in organization)
                {
                    organization_script.AppendLine($"({data.Cid}, '{data.Uuid}', '{data.Name}', '{data.ShortName}', '{data.LegalAddress}', '{data.ActualAddress}', '{data.PostalAddress}', '{data.Inn}', '{data.Kpp}', '{data.Bik}', '{data.Ogrn}', '{data.Okpo}', '{data.NcageOid}', '{data.IuOrganizationCodeOid}', '{data.DecimalOid}', '{data.Ieee1451ManufacturerOid}'),");
                }

                // Удаляем последнюю запятую в последней строке вставки данных
                organization_script.Length -= 3;

                // Добавляем символ ";" в конец строки
                organization_script.AppendLine(";");

                string script_insert_organization = organization_script.ToString();

                //var json_organization = JsonConvert.SerializeObject(organization, Formatting.Indented);

                return script_insert_organization;
            }
        }

        public void InsertUserDB()
        {
            string connectionString = "Host=localhost;Port=5432;Database=VpEmptyDB;Username=postgres;Password=0000;";

            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                // Создаем объект команды с указанным скриптом и подключением
                NpgsqlCommand command = new NpgsqlCommand(db_insert.organization, connection);

                // Выполняем скрипт
                command.ExecuteNonQuery();

                connection.Close();
            }

        }
        /*
        public void InsertUserDB(DBShemaJSON db_shema_json)
        {
            int selectColums = 0;

            using (NpgsqlConnection connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=0000;Database=VpEmptyDB"))
            {
                connection.Open();

                var tedsData = JsonConvert.DeserializeObject<List<TedsDatum>>(db_shema_json.teds_data);

                // Вставка данных в таблицу базы данных
                for (int i = 0; i < tedsData.Count; i++)
                {
                    TedsDatum? item = tedsData[i];
                    // Создание новой записи в таблице базы данных и заполнение полей
                    var newItem = new YourTableEntity
                    {
                        Field1 = item.Field1,
                        Field2 = item.Field2,
                        // ...
                    };

                    // Добавление новой записи в контекст базы данных
                    dbContext.YourTable.Add(newItem);
                }

                // Сохранение изменений в базе данных
                dbContext.SaveChanges();

            }

                // Десериализация JSON в объект типа TedsDatum
                List<TedsDatum> teds_data;
            try
            {
                teds_data = JsonConvert.DeserializeObject<List<TedsDatum>>(db_shema_json.teds_data);
            }
            catch (JsonException ex)
            {
                // Обработка ошибки десериализации
                labelFindSerial.Text = "Ошибка десериализации JSON: " + ex.Message;
                return;
            }

            // Команду для выборки данных из таблицы
            // Замените mytable, column1, column2 на соответствующие имена таблицы и столбцов
            string selectCommand = "SELECT select teds_data.date, teds_data.sensor_oid from teds.teds_data WHERE column1 = @Value1 AND column2 = @Value2";
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                // Создание команды для выполнения запросов к базе данных
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    // Проверка наличия данных в таблице
                    command.CommandText = "SELECT COUNT(*) FROM teds.teds_data WHERE  teds_data.date = @Value1 AND teds_data.sensor_oid = @Value2"; // Замените your_table и column1, column2 на соответствующие имена таблицы и столбцов
                    //command.Parameters.AddWithValue("@Value1", teds_data.Date); 
                    //command.Parameters.AddWithValue("@Value2", teds_data.SensorOid); 

                    //количество строк, которые удовлетворяют заданным условиям запроса
                    int rowCount = Convert.ToInt32(command.ExecuteScalar());

                    if (rowCount == 0)
                    {
                        selectColums++;
                        // Если данные отсутствуют, выполните их запись в базу данных
                        
                        // Создайте команду для вставки данных в таблицу
                        string insertCommand = "INSERT INTO your_table (column1, column2) VALUES (@Value1, @Value2)"; // Замените your_table и column1, column2 на соответствующие имена таблицы и столбцов
                        using (NpgsqlCommand insertCommand = new NpgsqlCommand(insertCommand, connection))
                        {
                            // Добавьте параметры и их значения для вставки данных
                            insertCommand.Parameters.AddWithValue("@Value1", teds_data.Value1); // Замените Value1 на соответствующее свойство/значение из teds_data
                            insertCommand.Parameters.AddWithValue("@Value2", teds_data.Value2); // Замените Value2 на соответствующее свойство/значение из teds_data

                            // Выполните команду вставки данных
                            insertCommand.ExecuteNonQuery();
                        }
                    }
                }


            }
            
            //поиск по таблице teds_data базы VpEmptyDB
            string selectCommand = "SELECT column1, column2 FROM mytable WHERE condition";

            // подключение к пользовательской базе данных
            string connectionString = "Host=localhost;Username=postgres;Password=0000;Database=VpEmptyDB";
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();



                // Создайте команду для вставки данных в таблицу
                string insertCommand = "INSERT INTO mytable (id, name) VALUES (@Id, @Name)"; // Замените mytable на имя вашей таблицы
                using (NpgsqlCommand command = new NpgsqlCommand(insertCommand, connection))
                {
                    // Добавьте параметры и их значения для вставки данных
                    command.Parameters.AddWithValue("@Id", data.Id);
                    command.Parameters.AddWithValue("@Name", data.Name);
                    // Добавьте параметры и значения для остальных полей таблицы

                    // Выполните команду вставки данных
                    command.ExecuteNonQuery();
                }
            }

        }
        */
    }
}