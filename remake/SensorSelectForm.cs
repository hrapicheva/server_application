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

namespace Server_App_3._3
{
    public partial class SensorSelectForm : Form
    {
        public struct DBShemaJSON
        {
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

        private int[,] SerialCidDB; // переменная класса для хранения массива данных

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
        private void buttonFind_Click(object sender, EventArgs e)
        {

            bool IsErrorDBSerial = false;
            string UserMessageSerialFinding = string.Empty;
            labelFindSerial.Text = "Выполняется поиск по базе...";
            labelError.Text = string.Empty;
            
            try
            {
                string SerialSelection = textBoxInputSerial.Text;
                //преобразование данных номеров и интервалов номеров в массив
                int[] SerialNumbers = ParseIndexSelection(SerialSelection);
                //проствление флагов наличи в базе
                int[,] DBSerialFlags = FindSelectionIndex(SerialNumbers);
                // вывод данных нашлось в базе, не нашлось в базе
                labelFindSerial.Text = MessageSerialInfo(DBSerialFlags, ref IsErrorDBSerial);

            }
            catch (ArgumentException ex)
            {
                labelError.Text = ex.Message;
            }
        }

        //поиск введенных номеров изделий
        public int[,] FindSelectionIndex(int[] SerialNumbers)
        {
            int countAvailableSerial = 0;

            //преобразовать одномерный массив в двумерный массив
            int countIndex = SerialNumbers.Length;
            int[,] DBSerialFlag = new int[3, countIndex];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < countIndex; j++)
                {
                    if (i == 0)
                    {
                        DBSerialFlag[i, j] = SerialNumbers[j];
                    }
                    else DBSerialFlag[i, j] = 0;
                }
            }

            //поиск номеров по базе флаг присутствия в базе
            using (var dbContext = new VpDevaContext())
            {
                //запрос к serials на совпадение с данными массива
                var serial_cid = dbContext.Sensors.Select(sensor => new { sensor.Serial, sensor.Cid }).ToList();
                /*
                var query = dbContext.Sensors
                .Where(sensor => serialNumbers.Contains(sensor.Serial))
                .Select(sensor => new { sensor.Serial, sensor.Cid })
                .ToList();*/

                // Чтение номеров изделий из базы данных и проставление флагов наличия в массиве DBSerial
                foreach (var serial in serial_cid)
                {
                    for (int i = 0; i < DBSerialFlag.GetLength(1); i++)
                    {
                        if (serial.Serial == DBSerialFlag[0, i])
                        {
                            DBSerialFlag[1, i] = 1; // Проставление флага наличия
                            DBSerialFlag[2, i] = ((int)serial.Cid);
                            countAvailableSerial++;
                            break;
                        }
                    }
                }
            }

            //инициализация глобальной переменной
            SerialCidDB = new int[2, countAvailableSerial];

            return DBSerialFlag;
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

        //вывод информции о номерах, хранящихся в базе
        public string MessageSerialInfo(int[,] DBSerial_flags, ref bool IsErrorDBSerial)
        {
            string serialInfo = string.Empty;
            string availableSerial = "Доступные изделия:";
            string unavailableSerial = "Недоступные изделия:";
            int count_available_serial = 0;
            //отсутствие доступных изделий
            //вывод отсортированной таблицей доступные/недоступные номера с прокруткой
            for (int i = 0; i < DBSerial_flags.GetLength(1); i++)
            {
                if (DBSerial_flags[1, i] == 1)
                {
                    availableSerial += DBSerial_flags[0, i].ToString() + ", ";

                    SerialCidDB[0, count_available_serial] = DBSerial_flags[0, i]; //запись serial в глобальную переменную
                    SerialCidDB[1, count_available_serial] = DBSerial_flags[2, i]; //запись cid в глобальную переменную
                    count_available_serial++;
                }
            }
            if (availableSerial != "Доступные изделия:") buttonPrint.Enabled = true;
            else buttonPrint.Enabled = false;

            return availableSerial;
        }
        private void buttonPrint_Click(object sender, EventArgs e)
        {
            labelInfoDB.Text = string.Empty;
            //данные из таблиц храним в структуре
            //сформировать скрипт
            DBShemaJSON db_shema_json = ScriptJSON();
            InsertUserDB(db_shema_json);
            //при вставке смотрим существуют ли данные с таким заводским номером и датой и исходя из этого пропускаем/вставляем данные
            //проверка версии базы данных
        }
        public DBShemaJSON ScriptJSON()
        {
            DBShemaJSON db_shema_json;
            labelInfoDB.Text = "Выполняется сбор данных...";

            var serials = new List<long>();
            var sensor_cid = new List<long?>();

            using (var dbContext = new VpDevaContext())
            {
                // Получаем список значений serial из массива Serial_Cid_DB
                for (int j = 0; j < SerialCidDB.Length / 2; j++)
                {
                    serials.Add(SerialCidDB[0, j]);
                    sensor_cid.Add(SerialCidDB[1, j]);
                }

                // Записываем JSON-строку таблицы Sensor в файл и переменную
                db_shema_json.sensor = SerialJSON(serials); //json sensor
                //Сбор данных по teds_data.sensor_oid == sensor.cid в таблице teds_data
                db_shema_json.teds_data = TedsJSON(sensor_cid); //json teds

                // Записываем JSON-строку таблиц, связанных с teds_data в структуру db_shema_json
                //Для выбранных teds_data.sensor_oid сбор данных по
                //type_cal_oid в соответствии с sensor_oid из таблицы type_call
                //teds_template_oid в соответствии с sensor_oid из таблицы teds_template
                //author_oid в соответствии с sensor_oid из таблицы author
                //manufacturer_oid в соответствии с sensor_oid из таблицы manufacturer
                //default_teds_parametrs_oid в соответствии с sensor_oid из таблицы default_teds_parametrs
                db_shema_json.default_teds_parametrs = DefaultTedsParametrsJSON(sensor_cid);
                db_shema_json.teds_employee = TedsEmployeeJSON(sensor_cid);
                db_shema_json.teds_template = TedsTemplateJSON(sensor_cid);
                db_shema_json.type_cal = TypeCalJSON(sensor_cid);
                db_shema_json.organization = OrganizationJSON(sensor_cid);
            }
            return db_shema_json;
        }
        public List<TedsDatum> TedsData(List<long?> sensor_oid)
        {
            using (var dbContext = new VpDevaContext())
            {
                var teds_data = dbContext.TedsData
                .Where(teds => sensor_oid.Contains(teds.SensorOid))
                .ToList();
                return teds_data;
            }
        }
        public string SerialJSON(List<long> serials)
        {
            string tableName = "Sensor";

            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку данных из таблицы Sensors на основе значений serial
                var sensor_data = dbContext.Sensors
                    .Where(sensor => serials.Contains(sensor.Serial))
                    .Select(sensor => new
                    {
                        sensor.Cid,
                        sensor.Uuid,
                        sensor.Serial,
                        sensor.VariantSensorOid,
                        sensor.AuthorOid
                    })
                    .ToList();

                // Сериализуем данные в JSON
                string json_serial = JsonConvert.SerializeObject(sensor_data, Formatting.Indented);

                // Создаем строку для хранения скрипта вставки данных в формате JSON
                string insertScriptSeral = $"INSERT INTO {tableName} (column_name) VALUES ('{json_serial}')";

                // Записываем JSON-строку в файл
                //File.WriteAllText(filePathInsertSensor, insertScriptSeral);
                return json_serial;
            }
        }
        public string TedsJSON(List<long?> sensor_cid)
        {
            //string tableName = "teds_data";

            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку данных из таблицы teds_data на основе значений serial
                var teds_data = dbContext.TedsData
                .Where(teds => sensor_cid.Contains(teds.SensorOid))
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

                // Сериализуем данные в JSON
                var json_teds_data = JsonConvert.SerializeObject(teds_data, Formatting.Indented);

                // Создаем строку для хранения скрипта вставки данных в формате JSON
                //string insertScriptTedsData = $"INSERT INTO {tableName} (column_name) VALUES ('{json_teds_data}')";
                //File.WriteAllText(filePathInsertSensor, insertScriptTedsData);
                return json_teds_data;
            }
        }
        public string DefaultTedsParametrsJSON(List<long?> sensor_oid)
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData(sensor_oid);

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

                var json_default_teds_parametrs = JsonConvert.SerializeObject(default_teds_parametrs, Formatting.Indented);

                return json_default_teds_parametrs;
            }
        }
        public string TedsEmployeeJSON(List<long?> sensor_oid)
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData(sensor_oid);

                // Получаем список связанных идентификаторов из таблицы teds_employee
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

                var json_teds_employee = JsonConvert.SerializeObject(teds_employee, Formatting.Indented);

                return json_teds_employee;
            }

        }
        public string TedsTemplateJSON(List<long?> sensor_oid)
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData(sensor_oid);

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

                var json_teds_template = JsonConvert.SerializeObject(teds_template, Formatting.Indented);

                return json_teds_template;
            }
        }
        public string TypeCalJSON(List<long?> sensor_oid)
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData(sensor_oid);

                // Получаем список связанных идентификаторов из таблицы teds_employee
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

                var json_type_cal = JsonConvert.SerializeObject(type_cal, Formatting.Indented);

                return json_type_cal;
            }
        }
        public string OrganizationJSON(List<long?> sensor_oid)
        {
            using (var dbContext = new VpDevaContext())
            {
                // Выполняем выборку строк из таблицы teds_data, где sensor_oid совпадает со значениями из списка
                var teds_data = TedsData(sensor_oid);

                // Получаем список связанных идентификаторов из таблицы teds_employee
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

                var json_organization = JsonConvert.SerializeObject(organization, Formatting.Indented);

                return json_organization;
            }
        }
        public void InsertUserDB(DBShemaJSON db_shema_json)
        {
            int selectColums = 0;


            string connectionString = "Host=localhost;Username=postgres;Password=0000;Database=VpEmptyDB";
            // Десериализация JSON в объект типа TedsDatum
            List <TedsDatum> teds_data;
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
                        /*
                        // Создайте команду для вставки данных в таблицу
                        string insertCommand = "INSERT INTO your_table (column1, column2) VALUES (@Value1, @Value2)"; // Замените your_table и column1, column2 на соответствующие имена таблицы и столбцов
                        using (NpgsqlCommand insertCommand = new NpgsqlCommand(insertCommand, connection))
                        {
                            // Добавьте параметры и их значения для вставки данных
                            insertCommand.Parameters.AddWithValue("@Value1", teds_data.Value1); // Замените Value1 на соответствующее свойство/значение из teds_data
                            insertCommand.Parameters.AddWithValue("@Value2", teds_data.Value2); // Замените Value2 на соответствующее свойство/значение из teds_data

                            // Выполните команду вставки данных
                            insertCommand.ExecuteNonQuery();
                        }*/
                    }
                }
            }
            /*
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
            }*/
        }
    }
}
