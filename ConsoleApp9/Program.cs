using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

public class UserData
{
    public string Name { get; set; }
    public int CharactersPerMinute { get; set; }
    public double CharactersPerSecond { get; set; }
}

public static class Leaderboard
{
    private const string LeaderboardFileName = "leaderboard.json";
    private static List<UserData> leaderboardData;
    private static readonly object leaderboardLock = new object();

    static Leaderboard()
    {
        leaderboardData = LoadLeaderboard();
    }

    public static void AddToLeaderboard(UserData userData)
    {
        lock (leaderboardLock)
        {
            leaderboardData.Add(userData);
            SaveLeaderboard();
        }
    }

    public static void DisplayLeaderboard()
    {
        Console.WriteLine("\nТаблица лидеров:");

        lock (leaderboardLock)
        {
            foreach (var userData in leaderboardData)
            {
                Console.WriteLine($"Имя: {userData.Name}, CPM: {userData.CharactersPerMinute}, CPS: {userData.CharactersPerSecond:F2}");
            }
        }
    }

    private static List<UserData> LoadLeaderboard()
    {
        try
        {
            if (File.Exists(LeaderboardFileName))
            {
                string json = File.ReadAllText(LeaderboardFileName);
                return JsonConvert.DeserializeObject<List<UserData>>(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при чтении таблицы лидеров: {ex.Message}");
        }

        return new List<UserData>();
    }

    private static void SaveLeaderboard()
    {
        try
        {
            string json = JsonConvert.SerializeObject(leaderboardData, Formatting.Indented);
            File.WriteAllText(LeaderboardFileName, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при записи в таблицу лидеров: {ex.Message}");
        }
    }
}

public class TypingLogic
{
    private const string TestText = "Пингвины это удивительные создания. Они живут в холодных водах Антарктики и Арктики, где плавают, словно собираются на встречу льдам и волнам. Их черно-белое оперение делает их настоящими красавцами природы.";
    private const int TestDurationMinutes = 1;

    private string userName;
    private bool inputAllowed;
    private readonly object locker = new object();

    public TypingLogic(string name)
    {
        userName = name;
        inputAllowed = true;
    }

    public bool GetInputAllowed()
    {
        return inputAllowed;
    }

    public void RunTest()
    {
        Console.WriteLine($"Привет, {userName}! Нажмите Enter, чтобы начать тест на скорость набора.");
        Console.ReadLine();

        Console.Clear();
        Console.WriteLine($"Привет, {userName}! Начнем тест на скорость набора. Наберите следующий текст:\n");
        Console.WriteLine(TestText);

        var userInput = string.Empty;
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var timerThread = new Thread(() => TimerThread(stopwatch));
        timerThread.Start();

        int currentIndex = 0;

        while (currentIndex < TestText.Length && stopwatch.Elapsed.TotalMinutes < TestDurationMinutes && inputAllowed)
        {
            DisplayTestText(currentIndex);

            var key = Console.ReadKey(true);

            if (key.KeyChar == TestText[currentIndex])
            {
                userInput += key.KeyChar;
                currentIndex++;
            }
            else
            {
                DisplayIncorrectCharacter(currentIndex);
            }
        }

        stopwatch.Stop();
        timerThread.Join();
        CalculateAndDisplayResults(userInput, stopwatch.Elapsed.TotalSeconds);
    }

    private void TimerThread(Stopwatch stopwatch)
    {
        try
        {
            while (stopwatch.Elapsed.TotalMinutes < TestDurationMinutes && inputAllowed)
            {
                DisplayRemainingTime(stopwatch.Elapsed);
                Thread.Sleep(1000);
            }
        }
        finally
        {
            Monitor.Exit(locker);
        }
    }

    private void DisplayTestText(int currentIndex)
    {
        lock (locker)
        {
            Console.SetCursorPosition(0, 2);

            Console.Write($"Привет, {userName}! Начнем тест на скорость набора. Наберите следующий текст:\n");

            for (int i = 0; i < TestText.Length; i++)
            {
                if (i == currentIndex)
                {
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.BackgroundColor = ConsoleColor.Green;
                }

                Console.Write(TestText[i]);

                if (i == currentIndex)
                {
                    Console.ResetColor();
                }
            }
        }
    }

    private void DisplayRemainingTime(TimeSpan elapsed)
    {
        lock (locker)
        {
            Console.SetCursorPosition(0, Console.WindowHeight - 1);
            TimeSpan remainingTime = TimeSpan.FromMinutes(TestDurationMinutes) - elapsed;
            Console.Write($"Оставшееся время: {remainingTime:mm\\:ss}");
        }
    }

    private void CalculateAndDisplayResults(string userInput, double elapsedTime)
    {
        inputAllowed = false;

        int correctCharacters = userInput.Length;
        double charactersPerSecond = correctCharacters / elapsedTime;
        int charactersPerMinute = (int)(charactersPerSecond * 60);

        Console.Clear();
        Console.WriteLine($"\nРезультаты для {userName}:");
        Console.WriteLine($"Время: {elapsedTime:F2} секунд");
        Console.WriteLine($"Количество символов в минуту: {charactersPerMinute}");
        Console.WriteLine($"Количество символов в секунду: {charactersPerSecond:F2}");

        var userData = new UserData
        {
            Name = userName,
            CharactersPerMinute = charactersPerMinute,
            CharactersPerSecond = charactersPerSecond
        };

        Leaderboard.AddToLeaderboard(userData);
        Leaderboard.DisplayLeaderboard();
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("Введите ваше имя:");
        string userName = Console.ReadLine();

        TypingLogic typingLogic = new TypingLogic(userName);

        try
        {
            do
            {
                typingLogic.RunTest();

                Console.WriteLine("\nХотите пройти тест еще раз? (да/нет):");
                string repeat = Console.ReadLine();

                if (repeat.ToLower() == "да")
                {
                    typingLogic = new TypingLogic(userName);
                    Console.WriteLine($"Привет, {userName}! Нажмите Enter, чтобы начать тест на скорость набора.");
                    Console.ReadLine();
                }
            } while (typingLogic.GetInputAllowed());
        }
        finally
        {
            Leaderboard.DisplayLeaderboard();
        }
    }
}
