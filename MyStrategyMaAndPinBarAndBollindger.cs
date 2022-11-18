using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsEngine.Robots.MyStrategyMaAndPinBarAndBollindger;

public class MyStrategyMaAndPinBarAndBollindger : BotPanel
{
    //Отĸрытие
    //1. цена выше нижней границы боллинджер и Скользящей средней
    //2 подтверждение пинбар на уровнях

    //закрытие 
    //стоп-лосс 2ATR
    
    private BotTabSimple _botTabSimple;
    private Atr _atr;
    private Bollinger _bollinger;
    private MovingAverage _movingAverage;
    public StrategyParameterInt Stop { get; set; }
    public StrategyParameterInt Profit { get; set; }
    public StrategyParameterInt Sleepage { get; set; }
    public int Value { get; set; }
    public StrategyParameterDecimal OptimalF { get; set; }
    public StrategyParameterString IsOnOff { get; set; }
    //public bool IsOnRobot { get; set; }
    public MyStrategyMaAndPinBarAndBollindger(string name, StartProgram startProgram) : base(name, startProgram)
    {
        IsOnOff = CreateParameter("Включить", "НЕТ", new string[] { "ДА", "НЕТ" });
        Stop = CreateParameter("Стоп", 20, 20, 100, 10);
        Profit = CreateParameter("Прибыль", 10, 50, 1000, 10);
        Sleepage = CreateParameter("Проскальзывание", 1, 2, 10, 1);
        OptimalF = CreateParameter("Коэффициент расчета плеча", 0.1m, 0.35m, 2, 0.1m);
        //IsOnRobot = true;

        // Load();

        TabCreate(BotTabType.Simple);
        _botTabSimple = TabsSimple[0];

        _bollinger = new Bollinger("Bollindger", false);
        _bollinger = (Bollinger)_botTabSimple.CreateCandleIndicator(_bollinger, "Prime");
        _bollinger.Save();

        _movingAverage = new MovingAverage("MA", false) { Lenght = 20, TypeCalculationAverage = MovingAverageTypeCalculation.Exponential, ColorBase = System.Drawing.Color.Coral };
        _movingAverage = (MovingAverage)_botTabSimple.CreateCandleIndicator(_movingAverage, "Prime");
        _movingAverage.Save();

        _atr = new Atr("ATR", false);
        _atr = (Atr)_botTabSimple.CreateCandleIndicator(_atr, "NewArea");
        _atr.Save();

        _botTabSimple.CandleFinishedEvent += _botTabSimple_CandleFinishedEvent;
        _botTabSimple.PositionOpeningSuccesEvent += _botTabSimple_PositionOpeningSuccesEvent;
    }


    private void _botTabSimple_PositionOpeningSuccesEvent(Position position)
    {

        List<Candle> candles = new List<Candle>();
        if (position.Direction == Side.Buy)
        {

            //стоп-лосс 2ATR
            _botTabSimple.CloseAtStop(
                 position,
                 position.EntryPrice - Stop.ValueInt * _botTabSimple.Securiti.PriceStep,
                 position.EntryPrice - Stop.ValueInt * _botTabSimple.Securiti.PriceStep - Sleepage.ValueInt * _botTabSimple.Securiti.PriceStep - 2 * _atr.Values[_atr.Values.Count - 1]);
            //тейк - профит
            _botTabSimple.CloseAtProfit(
               position,
               position.EntryPrice + Profit.ValueInt * _botTabSimple.Securiti.PriceStep,
               position.EntryPrice + Profit.ValueInt * _botTabSimple.Securiti.PriceStep + Sleepage.ValueInt * _botTabSimple.Securiti.PriceStep);

        }

        if (position.Direction == Side.Sell)
        {

            //стоп-лосс 2ATR
            _botTabSimple.CloseAtStop(
                position,
                position.EntryPrice + Stop.ValueInt * _botTabSimple.Securiti.PriceStep,
                position.EntryPrice + Stop.ValueInt * _botTabSimple.Securiti.PriceStep + Sleepage.ValueInt * _botTabSimple.Securiti.PriceStep + 2 * _atr.Values[_atr.Values.Count - 1]);

            //тейк - профит
            _botTabSimple.CloseAtProfit(
               position,
               position.EntryPrice - Profit.ValueInt * _botTabSimple.Securiti.PriceStep,
               position.EntryPrice - Profit.ValueInt * _botTabSimple.Securiti.PriceStep - Sleepage.ValueInt * _botTabSimple.Securiti.PriceStep);

        }




    }
    private void _botTabSimple_CandleFinishedEvent(List<Candle> candles)
    {

        List<Position> positions = _botTabSimple.PositionsOpenAll;
        //если робот отключен
        if (IsOnOff.ValueString != "ДА" /*|| IsOnRobot == false*/)
        {
            return;
        }
        //если длина MA, ATR больше количества свечей, то ни чего не делаем
        if (_bollinger.Lenght > candles.Count ||
            _movingAverage.Lenght > candles.Count ||
            _atr.Lenght > candles.Count)
        {
            return;
        }
        //если время старта меньше 11 часов, но ничего не делаем, т.е робот начнет работать с 11 утра
        //утренний гып сильно влияет на тестирование!!!!
        if (candles[candles.Count - 1].TimeStart.Hour < 11 || candles[candles.Count - 1].TimeStart.DayOfWeek == DayOfWeek.Friday && candles[candles.Count - 1].TimeStart.Hour > 17)
        {
            return;
        }

        //если есть открытые позиции и позиций больше 0, то ничего не делаем
        if (positions.Count > 0 && positions != null)
        {
            return;
        }

        //если позиция есть, но она не открылась
        if (positions.Count != 0 && positions != null)
        {
            if (positions[0].State != PositionStateType.Open)
            {
                return;
            }
        }



        //2 и цена возле нижней границы боллинджер и Скользящей средней.+ подтверждение пинбар на уровнях
        if (positions.Count == 0 || positions == null)
        {
            //long
            if (candles[candles.Count - 1].Close > _movingAverage.Values[_movingAverage.Values.Count - 1] ||
                candles[candles.Count - 1].Close > _bollinger.ValuesDown[_bollinger.ValuesDown.Count - 1] &&
                candles[candles.Count - 1].Close >= candles[candles.Count - 1].High - ((candles[candles.Count - 1].High - candles[candles.Count - 1].Low) / 3) &&
               candles[candles.Count - 1].Open >= candles[candles.Count - 1].High - ((candles[candles.Count - 1].High - candles[candles.Count - 1].Low) / 3)) //подтверждение пинбар на уровнях
            {
                decimal percent50RollbackOfThePinBarTail = ((candles[candles.Count - 1].Close + candles[candles.Count - 1].Low) / 2);//для входа 50% откате от пинабра
                decimal depositValueNow = _botTabSimple.Portfolio.ValueCurrent;//текущая величина депозита
                decimal lastPriceInstrument = candles[candles.Count - 1].Close;//текущая цена инструмента
                Value = Convert.ToInt32(depositValueNow * OptimalF.ValueDecimal / lastPriceInstrument);//Расчет объема депозита для входа в позицию (коэф оптимаотное F по умолячанию равен 0,5)
                //Value=1;
                _botTabSimple.BuyAtLimit(Value, percent50RollbackOfThePinBarTail + Sleepage.ValueInt);
            }

            //если нужно, чтобы бот торговал в short
            //short
            //if (candles[candles.Count - 1].Close < _movingAverage.Values[_movingAverage.Values.Count - 1] || candles[candles.Count - 1].Close < _bollinger.ValuesUp[_bollinger.ValuesUp.Count - 1] &&
            //     candles[candles.Count - 1].Close <= candles[candles.Count - 1].Low + ((candles[candles.Count - 1].High - candles[candles.Count - 1].Low) / 3) &&
            //     candles[candles.Count - 1].Open <= candles[candles.Count - 1].Low + ((candles[candles.Count - 1].High - candles[candles.Count - 1].Low) / 3) /*&& candles[candles.Count - 1].Close < candles[candles.Count - 1].Open*/)
            //{
            //    decimal percent50RollbackOfThePinBarTail = ((candles[candles.Count - 1].Close + candles[candles.Count - 1].Low) / 2);//для входа 50% откате от пинабра
            //    decimal depositValueNow = _botTabSimple.Portfolio.ValueCurrent;//текущая величина депозита
            //    decimal lastPriceInstrument = candles[candles.Count - 1].Close;//текущая цена инструмента
            //    Value = Convert.ToInt32(depositValueNow * OptimalF / lastPriceInstrument);//Рсчет объема депозита для входа в позицию (коэф оптимаотное F по умолячанию равен 0,5)
            //    _botTabSimple.SellAtLimit(Value, percent50RollbackOfThePinBarTail - Sleepage);
            //}



        }
    }
    //Сохранение настроек в файл и загрузка их через WPF(данная возможность убрана,т.к. настройки сделаны через параметры.)
    //public void Save()
    //{
    //    try
    //    {
    //        using (StreamWriter writer = new StreamWriter(/*@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"*/@"C:\TESTDIR\" + NameStrategyUniq + @"SettingsBot.txt"))
    //        {
    //            writer.WriteLine(Stop);
    //            writer.WriteLine(Sleepage);
    //            writer.WriteLine(Profit);
    //            writer.WriteLine(IsOnRobot);
    //            writer.WriteLine(OptimalF);
    //            writer.Close();
    //        }
    //    }
    //    catch (Exception)
    //    {

    //        //ignore
    //    }
    //}
    //public void Load()
    //{
    //    if (!File.Exists(/*@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"*/@"C:\TESTDIR\" + NameStrategyUniq + @"SettingsBot.txt"))
    //    {
    //        return;
    //    }
    //    try
    //    {
    //        using (StreamReader reader = new StreamReader(/*@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"*/@"C:\TESTDIR\" + NameStrategyUniq + @"SettingsBot.txt"))
    //        {
    //            Stop.ValueInt = Convert.ToInt32(reader.ReadLine());
    //            Sleepage.ValueInt = Convert.ToInt32(reader.ReadLine());
    //            Profit.ValueInt = Convert.ToInt32(reader.ReadLine());
    //            IsOnRobot = Convert.ToBoolean(reader.ReadLine());
    //            OptimalF.ValueDecimal = Convert.ToDecimal(reader.ReadLine());

    //            reader.Close();
    //        }
    //    }
    //    catch (Exception)
    //    {

    //        //ignore
    //    }
    //}
    public override string GetNameStrategyType()
    {
        return "MyStrategyMaAndPinBarAndBollindger";
    }

    public override void ShowIndividualSettingsDialog()
    {
        //MyStrategyMaAndPinBarAndBollindgerUi myStrategyMaAndPinBarAndBollindgerUi = new MyStrategyMaAndPinBarAndBollindgerUi(this);
        //myStrategyMaAndPinBarAndBollindgerUi.ShowDialog();
    }

}
