using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace lab5_2
{

    public class Triple
    {
        public int threadId;
        public string status;
        public long finishMs;

        public Triple(int threadId, string status, long finishMs)
        {
            this.threadId = threadId;
            this.status = status;
            this.finishMs = finishMs;
        }

    }

    public class Logger
    {
        static Mutex mutex = new Mutex(false);
        static Mutex mutex22 = new Mutex(false);
        static Logger(){
            FileStream fs = new FileStream("log_times.txt", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            fs.Close();
        }

        static public void ClearFileRec()
        {
            mutex22.WaitOne();
            FileStream fs = new FileStream("log_times.txt", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            fs.Close();
            mutex22.ReleaseMutex();
        }

        static public void WriteToF(string text)
        {
            mutex.WaitOne();
            using (FileStream file = new FileStream("log_times.txt", FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (StreamWriter sw = new StreamWriter(file)){
                sw.Write($"{text}\n");
            }
            mutex.ReleaseMutex();
        }
    }

    public class ObservableData<T> : INotifyPropertyChanged
    {
        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Refresh()
        {
            OnPropertyChanged();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window
    {
        List<Task<long>> tasks = null;
        static Mutex mutexSync = new Mutex(false);
        static Mutex mutex = new Mutex(false);
        static int ended = -1;
        static int s = -1;//каунтер кількості потоків які почали роботу та готові виконувати запис

        ObservableData<Triple> liveData = new ObservableData<Triple>();
        ObservableData<int> liveDataThreads = new ObservableData<int>();

        static ManualResetEvent startEvent = new ManualResetEvent(false);

        static long WriteToFileThread(object threadN)
        {
            s--; //при запуску зменшує каунтер

            startEvent.WaitOne(); //та тут чекає команди, коли всі інші потоки запустяться та почнуть виконання

            Stopwatch sw = Stopwatch.StartNew();

            int threadNum = (int)threadN;

            Random rnd = new Random(Thread.CurrentThread.ManagedThreadId);


            int randTime;



            Thread.Sleep(randTime = rnd.Next(1000, 3001));

            mutex.WaitOne();

            sw.Stop();

            Logger.WriteToF($"thread num {threadNum} finished in {sw.ElapsedMilliseconds} ms. Delay at start is {randTime}");

            mutex.ReleaseMutex();

            return sw.ElapsedMilliseconds;
        }

        void CreateThreadsAndLaunch(int count)
        {
            int temp_count = count;
            s = temp_count; //передаємо повну кількість потоків які будуть створені
            tasks = new List<Task<long>>();
            ended = temp_count;
            for (int i = 0; i < count; i++)
            {
                int num = i;
                liveData.Value = new Triple(num, "launching", -1);
                tasks.Add(new Task<long>(() => WriteToFileThread(num + 1)));
                tasks[i].ContinueWith(t => {
                    liveData.Value = new Triple(num, "completed", t.Result);
                    ended--;
                });

            }

            for (int i = 0; i < count; i++)
            {
                tasks[i].Start();
                int num = i;
                liveData.Value = new Triple(num, "waiting sync start", -1);
            }

            Task.Run(() =>
            {
                SpinWait.SpinUntil(() => ended == 0);
                Application.Current.Dispatcher.Invoke(() => launchButton.IsEnabled = true);
            });

            // по суті тепер всі потоки запускаються одночасно і порядок запису в файл корректний, як і очікувалося
            Task.Run(() => // алгоритм який реалізує одночасний старт виконання потоків
            {
                SpinWait.SpinUntil(() => s == 0); //очікує поки всі потоки зроблять деримент, каунтер тоді стане дорівнювати нулю і виконається цей алгоритм
                Logger.ClearFileRec();
                startEvent.Set();// тут надає дозвіл до виконання тому всі потоки з режиму очікування переходять до запису
                for (int i = 0; i < temp_count; i++)
                {
                    int num = i;
                    liveData.Value = new Triple(num, "in process", -1);
                }
            });
        }

        int threadCountToLaunch = 5;

        public MainWindow()
        {
            InitializeComponent();

            List<Label> statusLabels = new List<Label>();
            List<Label> msLabels = new List<Label>();

            liveDataThreads.PropertyChanged += (sender, args) => {

                statusLabels.Clear();
                msLabels.Clear();
                listb.Items.Clear();

                for (int i = 0; i < liveDataThreads.Value; i++)
                {
                    Grid grd = new Grid();

                    grd.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    grd.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    grd.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    grd.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    Label lab = new Label();
                    lab.Content = $"Thread num {i + 1} ";
                    lab.VerticalAlignment = VerticalAlignment.Center;
                    lab.HorizontalAlignment = HorizontalAlignment.Left;
                    Grid.SetColumn(lab, 0);
                    grd.Children.Add(lab);

                    Label lab1 = new Label();
                    lab1.Content = "status: ";
                    lab1.VerticalAlignment = VerticalAlignment.Center;
                    lab1.HorizontalAlignment = HorizontalAlignment.Left;
                    Grid.SetColumn(lab1, 1);
                    grd.Children.Add(lab1);

                    Label lab2 = new Label();
                    lab2.Content = "...";
                    lab2.Foreground = System.Windows.Media.Brushes.BlanchedAlmond;
                    lab2.VerticalAlignment = VerticalAlignment.Center;
                    lab2.HorizontalAlignment = HorizontalAlignment.Left;
                    Grid.SetColumn(lab2, 2);
                    statusLabels.Add(lab2);
                    grd.Children.Add(lab2);

                    Label lab3 = new Label();
                    lab3.Content = "";
                    lab3.VerticalAlignment = VerticalAlignment.Center;
                    lab3.HorizontalAlignment = HorizontalAlignment.Center;
                    Grid.SetColumn(lab3, 3);
                    msLabels.Add(lab3);
                    grd.Children.Add(lab3);

                    grd.Background = Brushes.WhiteSmoke;

                    Border brd = new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(2) };

                    brd.Child = grd;

                    listb.Items.Add(brd);
                }

            };



            liveData.PropertyChanged += (sender, e) => {



                if (Application.Current.Dispatcher.CheckAccess())
                {
                    Label labelToUpdate = statusLabels.ElementAt<Label>(liveData.Value.threadId);
                    if (liveData.Value.status == "launching")
                    {
                        labelToUpdate.Content = liveData.Value.status;
                        labelToUpdate.Foreground = Brushes.Gray;
                    }
                    else if (liveData.Value.status == "in process")
                    {
                        labelToUpdate.Content = liveData.Value.status;
                        labelToUpdate.Foreground = Brushes.OrangeRed;
                    }
                    else if (liveData.Value.status == "completed")
                    {
                        labelToUpdate.Content = liveData.Value.status;
                        labelToUpdate.Foreground = Brushes.DarkGreen;
                    }
                    else if (liveData.Value.status == "waiting sync start")
                    {
                        labelToUpdate.Content = liveData.Value.status;
                        labelToUpdate.Foreground = Brushes.DarkBlue;
                    }
                    else
                    {
                        labelToUpdate.Content = "unknown";
                        labelToUpdate.Foreground = Brushes.Red;
                    }

                    Label labelResult = msLabels.ElementAt<Label>(liveData.Value.threadId);

                    if (liveData.Value.finishMs == -1)
                    {
                        labelResult.Content = "waiting for result";
                    }
                    else
                    {
                        labelResult.Content = $"ended in {liveData.Value.finishMs} ms";
                    }
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Label labelToUpdate = statusLabels.ElementAt<Label>(liveData.Value.threadId);
                        if (liveData.Value.status == "launching")
                        {
                            labelToUpdate.Content = liveData.Value.status;
                            labelToUpdate.Foreground = Brushes.Gray;
                        }
                        else if (liveData.Value.status == "in process")
                        {
                            labelToUpdate.Content = liveData.Value.status;
                            labelToUpdate.Foreground = Brushes.OrangeRed;
                        }
                        else if (liveData.Value.status == "completed")
                        {
                            labelToUpdate.Content = liveData.Value.status;
                            labelToUpdate.Foreground = Brushes.DarkGreen;
                        }
                        else if (liveData.Value.status == "waiting sync start")
                        {
                            labelToUpdate.Content = liveData.Value.status;
                            labelToUpdate.Foreground = Brushes.DarkBlue;
                        }
                        else
                        {
                            labelToUpdate.Content = "unknown";
                            labelToUpdate.Foreground = Brushes.Red;
                        }

                        Label labelResult = msLabels.ElementAt<Label>(liveData.Value.threadId);

                        if (liveData.Value.finishMs == -1)
                        {
                            labelResult.Content = "waiting for result";
                        }
                        else
                        {
                            labelResult.Content = $"ended in {liveData.Value.finishMs} ms";
                        }
                    }
                    );
                }
                

            };


            
            launchButton.Click +=  (sender, args) => 
            {
                launchButton.IsEnabled = false;
                if (tasks != null) { Task<long>.WaitAll(tasks.ToArray());
                    foreach(Task<long> t in tasks)
                    {
                        t.Dispose();
                    }

                    tasks.Clear();

                }


                
                liveDataThreads.Value = threadCountToLaunch;
                CreateThreadsAndLaunch(threadCountToLaunch);
             
            };

            threadsCountSlider.ValueChanged += ThreadsCountSlider_ValueChanged;

        }


        private void ThreadsCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            counterText.Text = (threadCountToLaunch = (int)e.NewValue).ToString();
        }
    }
}
