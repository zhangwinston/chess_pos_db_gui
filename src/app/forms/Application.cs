﻿using chess_pos_db_gui.src.app;
using chess_pos_db_gui.src.app.chessdbcn;
using chess_pos_db_gui.src.chess;
using chess_pos_db_gui.src.chess.engine.analysis;
using chess_pos_db_gui.src.util;
using ChessDotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace chess_pos_db_gui
{
    public partial class Application : Form
    {
        private class ApplicationEmbeddedAnalysisHandler : EmbeddedAnalysisHandler
        {
            private readonly int embeddedAnalysisPanelHeight = 130;

            private readonly Application application;

            private EngineAnalysisForm ResponsibleEntity { get; set; }

            public ApplicationEmbeddedAnalysisHandler(Application application)
            {
                this.application = application;
            }

            public override System.Windows.Forms.Panel PrepareAndGetEmbeddedAnalysisPanel(EngineAnalysisForm form)
            {
                ResponsibleEntity = form;

                application.analysisAndBoardSplitContainer.SplitterDistance = embeddedAnalysisPanelHeight;
                application.analysisAndBoardSplitContainer.IsSplitterFixed = true;
                application.analysisAndBoardSplitContainer.FixedPanel = FixedPanel.Panel1;
                return application.analysisAndBoardSplitContainer.Panel1;
            }

            public override void OnEmbeddedAnalysisEnded()
            {
                application.analysisAndBoardSplitContainer.SplitterDistance = 0;
                application.analysisAndBoardSplitContainer.IsSplitterFixed = true;
                application.analysisAndBoardSplitContainer.FixedPanel = FixedPanel.Panel1;

                ResponsibleEntity = null;
            }

            public override void Dispose()
            {
                if (ResponsibleEntity != null)
                {
                    ResponsibleEntity.ForceStopEmbeddedAnalysis(this);
                }
            }
        }

        private class SerializableSettings : JsonSerializable<SerializableSettings>
        {
            public bool AutoQueryCheckBoxChecked { get; set; } = true;
            public bool QueryEvalCheckBoxChecked { get; set; } = true;
            public bool LevelHumanCheckBoxChecked { get; set; } = true;
            public bool LevelEngineCheckBoxChecked { get; set; } = true;
            public bool LevelServerCheckBoxChecked { get; set; } = true;
            public bool HideNeverPlayedCheckBoxChecked { get; set; } = false;
            public bool TypeContinuationsCheckBoxChecked { get; set; } = true;
            public bool TypeTranspositionsCheckBoxChecked { get; set; } = true;
            public bool HumanWeightCheckBoxChecked { get; set; } = true;
            public bool GamesWeightCheckBoxChecked { get; set; } = true;
            public bool EngineWeightCheckBoxChecked { get; set; } = true;
            public bool EvaluationWeightCheckBoxChecked { get; set; } = true;
            public bool CombineHECheckBoxChecked { get; set; } = false;
            public bool GoodnessUseCountCheckBoxChecked { get; set; } = true;
            public bool GoodnessNormalizeCheckBoxChecked { get; set; } = true;
            public decimal GamesWeightNumericUpDownValue { get; set; } = 1;
            public decimal HumanWeightNumericUpDownValue { get; set; } = 1;
            public decimal EngineWeightNumericUpDownValue { get; set; } = 3;
            public decimal EvalWeightNumericUpDownValue { get; set; } = 2;
            public int SplitChessAndDataSplitterDistance { get; set; } = 428;
            public int FormWidth { get; set; } = 1075;
            public int FormHeight { get; set; } = 600;
        }

        private static readonly string engineProfilesPath = "data/engine_profiles.json";
        private static readonly string settingsPath = "data/appliction/settings.json";

        private UciEngineProfileStorage EngineProfiles { get; set; }

        private string DatabaseTcpClientIp { get; set; } = "127.0.0.1";
        private int DatabaseTcpClientPort { get; set; } = 1234;
        private DatabaseProxy Database { get; set; }

        private HashSet<GameLevel> Levels { get; set; }
        private HashSet<Select> Selects { get; set; }

        private QueryCacheEntry CacheEntry { get; set; }
        private DataTable TabulatedData { get; set; }
        private DataTable TotalTabulatedData { get; set; }

        private double BestGoodness { get; set; }
        private bool IsEntryDataUpToDate { get; set; } = false;

        private EngineAnalysisForm AnalysisForm { get; set; }

        private EmbeddedAnalysisHandler EmbeddedHandler { get; set; }

        private QueryExecutor QueryExecutor { get; set; }

        private int PrevFirstVisibleRow { get; set; } = -1;
        private int PrevFirstVisibleColumn { get; set; } = -1;

        public Application()
        {
            Levels = new HashSet<GameLevel>();
            Selects = new HashSet<Select>();
            CacheEntry = null;
            TabulatedData = new DataTable();
            TotalTabulatedData = new DataTable();

            EngineProfiles = new UciEngineProfileStorage(engineProfilesPath);

            BestGoodness = 0.0;

            InitializeComponent();

            levelHumanCheckBox.Checked = true;
            levelEngineCheckBox.Checked = true;
            levelServerCheckBox.Checked = true;
            typeContinuationsCheckBox.Checked = true;
            typeTranspositionsCheckBox.Checked = true;

            queryButton.Enabled = false;

            DoubleBuffered = true;

            gamesWeightCheckbox.Visible = false;
            gamesWeightNumericUpDown.Visible = false;

            TabulatedData.Columns.Add(new DataColumn("Move", typeof(MoveWithSan)));
            TabulatedData.Columns.Add(new DataColumn("Count", typeof(ulong)));
            TabulatedData.Columns.Add(new DataColumn("WinCount", typeof(ulong)));
            TabulatedData.Columns.Add(new DataColumn("DrawCount", typeof(ulong)));
            TabulatedData.Columns.Add(new DataColumn("LossCount", typeof(ulong)));
            TabulatedData.Columns.Add(new DataColumn("Perf", typeof(double)));
            TabulatedData.Columns.Add(new DataColumn("DrawPct", typeof(double)));
            TabulatedData.Columns.Add(new DataColumn("HumanPct", typeof(double)));
            TabulatedData.Columns.Add(new DataColumn("AvgEloDiff", typeof(double)));
            TabulatedData.Columns.Add(new DataColumn("AdjustedPerf", typeof(double)));
            TabulatedData.Columns.Add(new DataColumn("Eval", typeof(ChessDBCNScore)));
            TabulatedData.Columns.Add(new DataColumn("EvalPct", typeof(double)));
            TabulatedData.Columns.Add(new DataColumn("Goodness", typeof(double)));
            TabulatedData.Columns.Add(new DataColumn("White", typeof(string)));
            TabulatedData.Columns.Add(new DataColumn("Black", typeof(string)));
            TabulatedData.Columns.Add(new DataColumn("Result", typeof(string)));
            TabulatedData.Columns.Add(new DataColumn("Date", typeof(string)));
            TabulatedData.Columns.Add(new DataColumn("Eco", typeof(string)));
            TabulatedData.Columns.Add(new DataColumn("PlyCount", typeof(ushort)));
            TabulatedData.Columns.Add(new DataColumn("Event", typeof(string)));
            TabulatedData.Columns.Add(new DataColumn("IsOnlyTransposition", typeof(bool)));

            TotalTabulatedData.Columns.Add(new DataColumn("Move", typeof(string)));
            TotalTabulatedData.Columns.Add(new DataColumn("Count", typeof(ulong)));
            TotalTabulatedData.Columns.Add(new DataColumn("WinCount", typeof(ulong)));
            TotalTabulatedData.Columns.Add(new DataColumn("DrawCount", typeof(ulong)));
            TotalTabulatedData.Columns.Add(new DataColumn("LossCount", typeof(ulong)));
            TotalTabulatedData.Columns.Add(new DataColumn("Perf", typeof(double)));
            TotalTabulatedData.Columns.Add(new DataColumn("DrawPct", typeof(double)));
            TotalTabulatedData.Columns.Add(new DataColumn("HumanPct", typeof(double)));
            TotalTabulatedData.Columns.Add(new DataColumn("AvgEloDiff", typeof(double)));
            TotalTabulatedData.Columns.Add(new DataColumn("AdjustedPerf", typeof(double)));
            TotalTabulatedData.Columns.Add(new DataColumn("Eval", typeof(ChessDBCNScore)));
            TotalTabulatedData.Columns.Add(new DataColumn("EvalPct", typeof(double)));

            WinFormsControlUtil.MakeDoubleBuffered(entriesGridView);
            entriesGridView.DataSource = TabulatedData;

            WinFormsControlUtil.MakeDoubleBuffered(totalEntriesGridView);
            totalEntriesGridView.DataSource = TotalTabulatedData;

            totalEntriesGridView.Columns["Move"].Frozen = true;
            totalEntriesGridView.Columns["Move"].MinimumWidth = 50;
            totalEntriesGridView.Columns["Move"].HeaderText = "";
            totalEntriesGridView.Columns["Move"].ToolTipText = "The type of statistic displayed. See '?'";
            totalEntriesGridView.Columns["Count"].HeaderText = "N";
            totalEntriesGridView.Columns["Count"].MinimumWidth = 30;
            totalEntriesGridView.Columns["Count"].ToolTipText = "The total number of instances for this position.";
            totalEntriesGridView.Columns["WinCount"].HeaderText = "+";
            totalEntriesGridView.Columns["WinCount"].MinimumWidth = 30;
            totalEntriesGridView.Columns["WinCount"].ToolTipText = "The number of times white has won a game from this position.";
            totalEntriesGridView.Columns["DrawCount"].HeaderText = "=";
            totalEntriesGridView.Columns["DrawCount"].MinimumWidth = 30;
            totalEntriesGridView.Columns["DrawCount"].ToolTipText = "The number of times the game has been drawn from this position.";
            totalEntriesGridView.Columns["LossCount"].HeaderText = "-";
            totalEntriesGridView.Columns["LossCount"].MinimumWidth = 30;
            totalEntriesGridView.Columns["LossCount"].ToolTipText = "The number of times white has lost a game from this position.";
            totalEntriesGridView.Columns["Perf"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            totalEntriesGridView.Columns["Perf"].HeaderText = "Wh%";
            totalEntriesGridView.Columns["Perf"].ToolTipText = "The performance (success rate) for the side to move. Equal to (W+D/2)/(W+D+L).";
            totalEntriesGridView.Columns["Perf"].MinimumWidth = 42;
            totalEntriesGridView.Columns["AdjustedPerf"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            totalEntriesGridView.Columns["AdjustedPerf"].HeaderText = "AWh%";
            totalEntriesGridView.Columns["AdjustedPerf"].ToolTipText = "The performance (success rate) adjusted for average elo difference of the players. It is adjusted such that it is 50% if performance is equal to the performance expected from the average elo difference.";
            totalEntriesGridView.Columns["AdjustedPerf"].MinimumWidth = 48;
            totalEntriesGridView.Columns["DrawPct"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            totalEntriesGridView.Columns["DrawPct"].MinimumWidth = 30;
            totalEntriesGridView.Columns["DrawPct"].HeaderText = "D%";
            totalEntriesGridView.Columns["DrawPct"].ToolTipText = "The % of games that ended in a draw.";
            totalEntriesGridView.Columns["HumanPct"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            totalEntriesGridView.Columns["HumanPct"].MinimumWidth = 30;
            totalEntriesGridView.Columns["HumanPct"].HeaderText = "H%";
            totalEntriesGridView.Columns["HumanPct"].ToolTipText = "The % of games being played by humans (Human + Server).";
            totalEntriesGridView.Columns["AvgEloDiff"].MinimumWidth = 30;
            totalEntriesGridView.Columns["AvgEloDiff"].HeaderText = "ΔE";
            totalEntriesGridView.Columns["AvgEloDiff"].ToolTipText = "The average elo difference between two players. WhiteElo - BlackElo.";
            totalEntriesGridView.Columns["AvgEloDiff"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            totalEntriesGridView.Columns["Eval"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            totalEntriesGridView.Columns["Eval"].MinimumWidth = 30;
            totalEntriesGridView.Columns["Eval"].HeaderText = "Ev";
            totalEntriesGridView.Columns["Eval"].ToolTipText = "Engine evaluation. Based on noobpwnftw's https://chessdb.cn/queryc_en/";
            totalEntriesGridView.Columns["EvalPct"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            totalEntriesGridView.Columns["EvalPct"].MinimumWidth = 30;
            totalEntriesGridView.Columns["EvalPct"].HeaderText = "Ev%";
            totalEntriesGridView.Columns["EvalPct"].ToolTipText = "The expected performance based on evaluation.";

            totalEntriesGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader;

            entriesGridView.Columns["Move"].Frozen = true;
            entriesGridView.Columns["Move"].MinimumWidth = 50;
            entriesGridView.Columns["Move"].ToolTipText = "The move leading to the position for which stats are displayed.";
            entriesGridView.Columns["Count"].HeaderText = "N";
            entriesGridView.Columns["Count"].MinimumWidth = 30;
            entriesGridView.Columns["Count"].ToolTipText = totalEntriesGridView.Columns["Count"].ToolTipText;
            entriesGridView.Columns["WinCount"].HeaderText = "+";
            entriesGridView.Columns["WinCount"].MinimumWidth = 30;
            entriesGridView.Columns["WinCount"].ToolTipText = totalEntriesGridView.Columns["WinCount"].ToolTipText;
            entriesGridView.Columns["DrawCount"].HeaderText = "=";
            entriesGridView.Columns["DrawCount"].MinimumWidth = 30;
            entriesGridView.Columns["DrawCount"].ToolTipText = totalEntriesGridView.Columns["DrawCount"].ToolTipText;
            entriesGridView.Columns["LossCount"].HeaderText = "-";
            entriesGridView.Columns["LossCount"].MinimumWidth = 30;
            entriesGridView.Columns["LossCount"].ToolTipText = totalEntriesGridView.Columns["LossCount"].ToolTipText;
            entriesGridView.Columns["Perf"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["Perf"].HeaderText = "Wh%";
            entriesGridView.Columns["Perf"].ToolTipText = totalEntriesGridView.Columns["Perf"].ToolTipText;
            entriesGridView.Columns["Perf"].MinimumWidth = 42;
            entriesGridView.Columns["AdjustedPerf"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["AdjustedPerf"].HeaderText = "AWh%";
            entriesGridView.Columns["AdjustedPerf"].ToolTipText = totalEntriesGridView.Columns["AdjustedPerf"].ToolTipText;
            entriesGridView.Columns["AdjustedPerf"].MinimumWidth = 48;
            entriesGridView.Columns["DrawPct"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["DrawPct"].MinimumWidth = 30;
            entriesGridView.Columns["DrawPct"].HeaderText = "D%";
            entriesGridView.Columns["DrawPct"].ToolTipText = totalEntriesGridView.Columns["DrawPct"].ToolTipText;
            entriesGridView.Columns["HumanPct"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["HumanPct"].MinimumWidth = 30;
            entriesGridView.Columns["HumanPct"].HeaderText = "H%";
            entriesGridView.Columns["HumanPct"].ToolTipText = totalEntriesGridView.Columns["HumanPct"].ToolTipText;
            entriesGridView.Columns["AvgEloDiff"].MinimumWidth = 30;
            entriesGridView.Columns["AvgEloDiff"].HeaderText = "ΔE";
            entriesGridView.Columns["AvgEloDiff"].ToolTipText = totalEntriesGridView.Columns["AvgEloDiff"].ToolTipText;
            entriesGridView.Columns["AvgEloDiff"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["Result"].MinimumWidth = 30;
            entriesGridView.Columns["Result"].HeaderText = "";
            entriesGridView.Columns["Result"].ToolTipText = "The result of the first game with this position.";
            entriesGridView.Columns["Eco"].HeaderText = "ECO";
            entriesGridView.Columns["Eco"].ToolTipText = "The ECO code reported in the first game with this position.";
            entriesGridView.Columns["Eco"].MinimumWidth = 35;
            entriesGridView.Columns["Eco"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["PlyCount"].HeaderText = "Plies";
            entriesGridView.Columns["PlyCount"].ToolTipText = "The length in plies of the first game with this position.";
            entriesGridView.Columns["PlyCount"].MinimumWidth = 40;
            entriesGridView.Columns["PlyCount"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["Event"].MinimumWidth = 50;
            entriesGridView.Columns["Event"].ToolTipText = "Even at which this position was first achieved.";
            entriesGridView.Columns["White"].MinimumWidth = 50;
            entriesGridView.Columns["White"].ToolTipText = "White player for the first game achieving this position.";
            entriesGridView.Columns["Black"].MinimumWidth = 50;
            entriesGridView.Columns["Black"].ToolTipText = "Black player for the first game achieving this position.";
            entriesGridView.Columns["Date"].MinimumWidth = 40;
            entriesGridView.Columns["Date"].ToolTipText = "Date of the first game achieving this position.";
            entriesGridView.Columns["Eval"].MinimumWidth = 35;
            entriesGridView.Columns["Eval"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["Eval"].HeaderText = "Ev";
            entriesGridView.Columns["Eval"].ToolTipText = totalEntriesGridView.Columns["Eval"].ToolTipText;
            entriesGridView.Columns["EvalPct"].MinimumWidth = 35;
            entriesGridView.Columns["EvalPct"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["EvalPct"].HeaderText = "Ev%";
            entriesGridView.Columns["EvalPct"].ToolTipText = totalEntriesGridView.Columns["EvalPct"].ToolTipText;
            entriesGridView.Columns["Goodness"].HeaderText = "QI";
            entriesGridView.Columns["Goodness"].ToolTipText = "Quality Index. This value represents the calcualated quality of the move based on available data and user set weights.";
            entriesGridView.Columns["Goodness"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            entriesGridView.Columns["Goodness"].MinimumWidth = 40;
            entriesGridView.Columns["IsOnlyTransposition"].Visible = false;

            entriesGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader;

            entriesGridView.Sort(entriesGridView.Columns["Goodness"], ListSortDirection.Descending);

            totalDataHelpButton.Width = totalEntriesGridView.RowHeadersWidth;
            totalDataHelpButton.Height = totalEntriesGridView.ColumnHeadersHeight;

            WinFormsControlUtil.SetThousandSeparator(entriesGridView);
            WinFormsControlUtil.SetThousandSeparator(totalEntriesGridView);

            analysisAndBoardSplitContainer.SplitterDistance = 0;

            fenRichTextBox.Text = "FEN: " + FenProvider.StartPos;
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            Database?.Dispose();
        }

        private void Application_Load(object sender, EventArgs e)
        {
            LoadSettings();

            try
            {
                Database = new DatabaseProxy(DatabaseTcpClientIp, DatabaseTcpClientPort);
                Database.FetchSupportManifest();
                QueryExecutor = new QueryExecutor(Database);
                QueryExecutor.DataReceived += OnDataReceived;
            }
            catch
            {
                MessageBox.Show("Cannot establish communication with the database backend.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            chessBoard.PositionChanged += OnPositionChanged;

            UpdateDatabaseInfo();
        }

        private void OnDataReceived(object sender, KeyValuePair<QueryQueueEntry, QueryCacheEntry> p)
        {
            var key = p.Key;
            var data = p.Value;
            if (key.CurrentFen == chessBoard.GetFen())
            {
                CacheEntry = data;
                Invoke(new MethodInvoker(Repopulate));
            }
        }

        private void OnPositionChanged(object sender, EventArgs e)
        {
            var fen = chessBoard.GetFen();

            if (AnalysisForm != null)
            {
                AnalysisForm.OnPositionChanged(fen);
            }

            fenRichTextBox.Text = "FEN: " + fen;

            if (!Database.IsOpen)
            {
                return;
            }

            if (autoQueryCheckbox.Checked)
            {
                UpdateData();
                IsEntryDataUpToDate = true;
            }
            else
            {
                IsEntryDataUpToDate = false;
            }
        }

        private double CalculateGoodness(AggregatedEntry entry, AggregatedEntry nonEngineEntry, ChessDBCNScore score)
        {
            var options = new GoodnessCalculator.Options
            {
                UseHumanGames = humanWeightCheckbox.Checked,
                UseEngineGames = engineWeightCheckbox.Checked,
                UseEval = evaluationWeightCheckbox.Checked,
                UseCombinedGames = gamesWeightCheckbox.Checked,
                CombineGames = combineHECheckbox.Checked,
                UseCount = goodnessUseCountCheckbox.Checked,

                HumanWeight = (double)humanWeightNumericUpDown.Value,
                EngineWeight = (double)engineWeightNumericUpDown.Value,
                EvalWeight = (double)evalWeightNumericUpDown.Value,
                CombinedGamesWeight = (double)gamesWeightNumericUpDown.Value
            };

            return GoodnessCalculator.CalculateGoodness(
                chessBoard.SideToMove(),
                entry,
                nonEngineEntry,
                score,
                options
                );
        }

        private void NormalizeGoodnessValues()
        {
            double highest = GetBestGoodness();

            if (highest > 0.0)
            {
                foreach (DataRow row in TabulatedData.Rows)
                {
                    if (row["Goodness"] != null)
                    {
                        row["Goodness"] = (double)row["Goodness"] / highest;
                    }
                }
            }
        }

        private void PopulateFirstGameInfo(AggregatedEntry entry)
        {
            foreach (GameHeader header in entry.FirstGame)
            {
                firstGameInfoRichTextBox.Text = string.Format(
                    "{0} - {1} {2} [{3}] ({4})\n{5} {6}",
                    new object[]
                    {
                        header.White,
                        header.Black,
                        header.Result.ToStringPgnFormat(),
                        header.Eco,
                        header.PlyCount.Or(0) / 2,
                        header.Event,
                        header.Date.ToStringOmitUnknown()
                    }
                );
            }
        }

        private void PopulateCommon(
            DataRow row,
            AggregatedEntry total,
            AggregatedEntry totalNonEngine,
            ChessDBCNScore totalScore
            )
        {
            long maxDisplayedEloDiff = 400;

            row["Count"] = total.Count;
            row["WinCount"] = total.WinCount;
            row["DrawCount"] = total.DrawCount;
            row["LossCount"] = total.LossCount;

            var averageEloDiff = total.Count > 0 ? total.TotalEloDiff / (double)total.Count : 0.0;
            var expectedPerf = EloCalculator.GetExpectedPerformance(averageEloDiff);
            var adjustedPerf = EloCalculator.GetAdjustedPerformance(total.Perf, expectedPerf);
            if (chessBoard.SideToMove() == Player.White)
            {
                row["Perf"] = total.Perf;
                row["AdjustedPerf"] = adjustedPerf;
            }
            else
            {
                row["Perf"] = 1.0 - total.Perf;
                row["AdjustedPerf"] = 1.0 - adjustedPerf;
            }
            row["DrawPct"] = (total.DrawRate);
            row["HumanPct"] = (totalNonEngine.Count / (double)total.Count);

            row["AvgEloDiff"] =
                total.Count == 0
                ? double.NaN
                : Math.Min(maxDisplayedEloDiff, Math.Max(-maxDisplayedEloDiff, (long)Math.Round(averageEloDiff)));

            // score is always for side to move
            if (totalScore != null)
            {
                row["Eval"] = totalScore;
                row["EvalPct"] = totalScore.Perf;
            }
        }

        private void Populate(
            string san, 
            AggregatedEntry entry, 
            AggregatedEntry nonEngineEntry, 
            bool isOnlyTransposition, 
            ChessDBCNScore score
            )
        {
            var fen = chessBoard.GetFen();

            var row = TabulatedData.NewRow();

            PopulateCommon(row, entry, nonEngineEntry, score);

            row["Move"] = new MoveWithSan(San.ParseSan(fen, san), san);
            row["Goodness"] = CalculateGoodness(entry, nonEngineEntry, score);

            foreach (GameHeader header in entry.FirstGame)
            {
                row["Date"] = header.Date.ToStringOmitUnknown();
                row["Event"] = header.Event;
                row["White"] = header.White;
                row["Black"] = header.Black;
                row["Result"] = header.Result.ToStringPgnUnicodeFormat();
                row["Eco"] = header.Eco.ToString();
                row["PlyCount"] = header.PlyCount.FirstOrDefault();
            }

            row["IsOnlyTransposition"] = isOnlyTransposition;

            TabulatedData.Rows.Add(row);
        }

        private void PopulateTotal(
            string name,
            AggregatedEntry total, 
            AggregatedEntry totalNonEngine, 
            ChessDBCNScore totalScore
            )
        {
            var row = TotalTabulatedData.NewRow();

            PopulateCommon(row, total, totalNonEngine, totalScore);

            row["Move"] = name;

            TotalTabulatedData.Rows.InsertAt(row, 0);
        }

        private void UpdateGoodness(
            string move, 
            AggregatedEntry entry, 
            AggregatedEntry nonEngineEntry, 
            ChessDBCNScore score
            )
        {
            System.Data.DataRow row = null;
            foreach (System.Data.DataRow r in TabulatedData.Rows)
            {
                if (((MoveWithSan)r["Move"]).San == move)
                {
                    row = r;
                    break;
                }
            }
            if (row == null)
            {
                return;
            }

            row["Goodness"] = CalculateGoodness(entry, nonEngineEntry, score);
        }

        private bool IsEmpty(AggregatedEntry entry)
        {
            return entry.Count == 0;
        }

        private ChessDBCNScore GetBestScore(Dictionary<Move, ChessDBCNScore> scores)
        {
            ChessDBCNScore best = null;
            foreach (KeyValuePair<Move, ChessDBCNScore> entry in scores)
            {
                if (best == null || (entry.Value.Value > best.Value))
                {
                    best = entry.Value;
                }
            }
            return best;
        }

        private void Populate(
            Dictionary<string, AggregatedEntry> entries,
            IEnumerable<string> continuationMoves,
            Dictionary<string, AggregatedEntry> nonEngineEntries,
            Dictionary<Move, ChessDBCNScore> scores
            )
        {
            Clear();

            bool hideEmpty = hideNeverPlayedCheckBox.Checked;

            AggregatedEntry total = new AggregatedEntry();
            AggregatedEntry totalNonEngine = new AggregatedEntry();
            ChessDBCNScore bestScore = GetBestScore(scores);
            foreach (KeyValuePair<string, AggregatedEntry> entry in entries)
            {
                if (hideEmpty && IsEmpty(entry.Value))
                {
                    continue;
                }

                if (!nonEngineEntries.ContainsKey(entry.Key))
                {
                    nonEngineEntries.Add(entry.Key, new AggregatedEntry());
                }

                if (entry.Key == San.NullMove)
                {
                    PopulateTotal("Root", entry.Value, nonEngineEntries[entry.Key], bestScore);
                    PopulateFirstGameInfo(entry.Value);
                }
                else
                {
                    var fen = chessBoard.GetFen();
                    scores.TryGetValue(San.ParseSan(fen, entry.Key), out ChessDBCNScore score);
                    Populate(entry.Key, entry.Value, nonEngineEntries[entry.Key], !continuationMoves.Contains(entry.Key), score);

                    total.Combine(entry.Value);
                    totalNonEngine.Combine(nonEngineEntries[entry.Key]);
                }
            }

            PopulateTotal("Children", total, totalNonEngine, bestScore);

            if (goodnessNormalizeCheckbox.Checked)
            {
                NormalizeGoodnessValues();
            }

            UpdateBestGoodness();
        }

        private void UpdateGoodness(
            Dictionary<string, AggregatedEntry> entries,
            Dictionary<string, AggregatedEntry> nonEngineEntries,
            Dictionary<Move, ChessDBCNScore> scores
            )
        {
            entriesGridView.SuspendLayout();

            bool hideEmpty = hideNeverPlayedCheckBox.Checked;

            foreach (KeyValuePair<string, AggregatedEntry> entry in entries)
            {
                if (hideEmpty && IsEmpty(entry.Value))
                {
                    continue;
                }

                if (!nonEngineEntries.ContainsKey(entry.Key))
                {
                    nonEngineEntries.Add(entry.Key, new AggregatedEntry());
                }

                if (entry.Key != San.NullMove)
                {
                    var fen = chessBoard.GetFen();
                    scores.TryGetValue(San.ParseSan(fen, entry.Key), out ChessDBCNScore score);
                    UpdateGoodness(entry.Key, entry.Value, nonEngineEntries[entry.Key], score);
                }
            }

            if (goodnessNormalizeCheckbox.Checked)
            {
                NormalizeGoodnessValues();
            }

            UpdateBestGoodness();

            entriesGridView.ResumeLayout(false);

            entriesGridView.Refresh();
        }

        private void UpdateBestGoodness()
        {
            BestGoodness = GetBestGoodness();
        }

        private double GetBestGoodness()
        {
            double goodness = 0.0;

            foreach (DataRow row in TabulatedData.Rows)
            {
                if (row["Goodness"] != null)
                {
                    goodness = Math.Max(goodness, (double)row["Goodness"]);
                }
            }

            return goodness;
        }

        private void Gather(QueryCacheEntry res, Select select, List<GameLevel> levels, ref Dictionary<string, AggregatedEntry> aggregatedEntries)
        {
            var rootEntries = res.Stats.Results[0].ResultsBySelect[select].Root;
            var childrenEntries = res.Stats.Results[0].ResultsBySelect[select].Children;

            if (aggregatedEntries.ContainsKey("--"))
            {
                aggregatedEntries["--"].Combine(new AggregatedEntry(rootEntries, levels));
            }
            else
            {
                aggregatedEntries.Add("--", new AggregatedEntry(rootEntries, levels));
            }
            foreach (KeyValuePair<string, SegregatedEntries> entry in childrenEntries)
            {
                if (aggregatedEntries.ContainsKey(entry.Key))
                {
                    aggregatedEntries[entry.Key].Combine(new AggregatedEntry(entry.Value, levels));
                }
                else
                {
                    aggregatedEntries.Add(entry.Key, new AggregatedEntry(entry.Value, levels));
                }
            }
        }

        private void Populate(QueryCacheEntry res, List<Select> selects, List<GameLevel> levels)
        {
            Dictionary<string, AggregatedEntry> aggregatedContinuationEntries = new Dictionary<string, AggregatedEntry>();

            Gather(res, chess_pos_db_gui.Select.Continuations, levels, ref aggregatedContinuationEntries);

            Dictionary<string, AggregatedEntry> aggregatedEntries = new Dictionary<string, AggregatedEntry>();
            foreach (Select select in selects)
            {
                Gather(res, select, levels, ref aggregatedEntries);
            }

            Dictionary<string, AggregatedEntry> aggregatedNonEngineEntries = new Dictionary<string, AggregatedEntry>();
            if (levels.Contains(GameLevel.Human) || levels.Contains(GameLevel.Server))
            {
                var nonEngineLevels = new List<GameLevel> { };
                if (levels.Contains(GameLevel.Human))
                {
                    nonEngineLevels.Add(GameLevel.Human);
                }

                if (levels.Contains(GameLevel.Server))
                {
                    nonEngineLevels.Add(GameLevel.Server);
                }

                foreach (Select select in selects)
                {
                    Gather(res, select, nonEngineLevels, ref aggregatedNonEngineEntries);
                }
            }

            Populate(aggregatedEntries, aggregatedContinuationEntries.Where(p => p.Value.Count != 0).Select(p => p.Key), aggregatedNonEngineEntries, CacheEntry.Scores);
        }

        private void UpdateGoodness(QueryCacheEntry res, List<Select> selects, List<GameLevel> levels)
        {
            Dictionary<string, AggregatedEntry> aggregatedContinuationEntries = new Dictionary<string, AggregatedEntry>();

            Gather(res, chess_pos_db_gui.Select.Continuations, levels, ref aggregatedContinuationEntries);

            Dictionary<string, AggregatedEntry> aggregatedEntries = new Dictionary<string, AggregatedEntry>();
            foreach (Select select in selects)
            {
                Gather(res, select, levels, ref aggregatedEntries);
            }

            Dictionary<string, AggregatedEntry> aggregatedNonEngineEntries = new Dictionary<string, AggregatedEntry>();
            if (levels.Contains(GameLevel.Human) || levels.Contains(GameLevel.Server))
            {
                var nonEngineLevels = new List<GameLevel> { };
                if (levels.Contains(GameLevel.Human))
                {
                    nonEngineLevels.Add(GameLevel.Human);
                }

                if (levels.Contains(GameLevel.Server))
                {
                    nonEngineLevels.Add(GameLevel.Server);
                }

                foreach (Select select in selects)
                {
                    Gather(res, select, nonEngineLevels, ref aggregatedNonEngineEntries);
                }
            }

            UpdateGoodness(aggregatedEntries, aggregatedNonEngineEntries, CacheEntry.Scores);
        }

        private void Clear()
        {
            TabulatedData.Clear();
            TotalTabulatedData.Clear();

            entriesGridView.Refresh();
            totalEntriesGridView.Refresh();

            firstGameInfoRichTextBox.Clear();
        }

        private void Repopulate()
        {
            WinFormsControlUtil.SuspendDrawing(entriesGridView);

            SaveViewScroll();

            if (Selects.Count == 0 || Levels.Count == 0 || CacheEntry == null)
            {
                Clear();
            }
            else
            {
                if (chessBoard.SideToMove() == Player.White)
                {
                    entriesGridView.Columns["Perf"].HeaderText = "Wh%";
                    totalEntriesGridView.Columns["Perf"].HeaderText = "Wh%";
                    entriesGridView.Columns["AdjustedPerf"].HeaderText = "AWh%";
                    totalEntriesGridView.Columns["AdjustedPerf"].HeaderText = "AWh%";
                }
                else
                {
                    entriesGridView.Columns["Perf"].HeaderText = "Bl%";
                    totalEntriesGridView.Columns["Perf"].HeaderText = "Bl%";
                    entriesGridView.Columns["AdjustedPerf"].HeaderText = "ABl%";
                    totalEntriesGridView.Columns["AdjustedPerf"].HeaderText = "ABl%";
                }

                Populate(CacheEntry, Selects.ToList(), Levels.ToList());
            }

            entriesGridView.ClearSelection();
            ReloadViewScroll();

            WinFormsControlUtil.ResumeDrawing(entriesGridView);
        }

        private void ReloadViewScroll()
        {
            if (PrevFirstVisibleRow != -1)
            {
                entriesGridView.FirstDisplayedScrollingRowIndex = Math.Min(PrevFirstVisibleRow, entriesGridView.Rows.Count - 1);
            }
            else if (entriesGridView.Rows.Count > 0)
            {
                entriesGridView.FirstDisplayedScrollingRowIndex = 0;
            }

            if (PrevFirstVisibleColumn != -1)
            {
                entriesGridView.FirstDisplayedScrollingColumnIndex = PrevFirstVisibleColumn;
            }
        }

        private void SaveViewScroll()
        {
            if (entriesGridView.Rows.Count > 0)
            {
                PrevFirstVisibleColumn = entriesGridView.FirstDisplayedScrollingColumnIndex;
                PrevFirstVisibleRow = entriesGridView.FirstDisplayedScrollingRowIndex;
            }
        }

        private void UpdateGoodness()
        {
            WinFormsControlUtil.SuspendDrawing(entriesGridView);

            SaveViewScroll();

            if (Selects.Count == 0 || Levels.Count == 0 || CacheEntry == null)
            {
                WinFormsControlUtil.ResumeDrawing(entriesGridView);
                return;
            }
            else
            {
                UpdateGoodness(CacheEntry, Selects.ToList(), Levels.ToList());
            }

            entriesGridView.ClearSelection();

            WinFormsControlUtil.ResumeDrawing(entriesGridView);
        }

        private void LevelHumanCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (levelHumanCheckBox.Checked)
            {
                Levels.Add(GameLevel.Human);
            }
            else
            {
                Levels.Remove(GameLevel.Human);
            }

            Repopulate();
        }

        private void LevelEngineCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (levelEngineCheckBox.Checked)
            {
                Levels.Add(GameLevel.Engine);
            }
            else
            {
                Levels.Remove(GameLevel.Engine);
            }

            Repopulate();
        }

        private void LevelServerCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (levelServerCheckBox.Checked)
            {
                Levels.Add(GameLevel.Server);
            }
            else
            {
                Levels.Remove(GameLevel.Server);
            }

            Repopulate();
        }

        private void TypeContinuationsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (typeContinuationsCheckBox.Checked)
            {
                Selects.Add(chess_pos_db_gui.Select.Continuations);
            }
            else
            {
                Selects.Remove(chess_pos_db_gui.Select.Continuations);
            }

            Repopulate();
        }

        private void TypeTranspositionsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (typeTranspositionsCheckBox.Checked)
            {
                Selects.Add(chess_pos_db_gui.Select.Transpositions);
            }
            else
            {
                Selects.Remove(chess_pos_db_gui.Select.Transpositions);
            }

            Repopulate();
        }

        private void AutoQueryCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (autoQueryCheckbox.Checked)
            {
                queryButton.Enabled = false;
                UpdateData();
            }
            else
            {
                queryButton.Enabled = true;
            }
        }

        private void QueryButton_Click(object sender, EventArgs e)
        {
            UpdateData();
        }

        private void ScheduleUpdateDataAsync()
        {
            var sig = new QueryQueueEntry(chessBoard, queryEvalCheckBox.Checked);
            QueryExecutor.ScheduleUpdateDataAsync(sig);
        }

        private void UpdateData()
        {
            if (!Database.IsOpen)
            {
                return;
            }

            SaveViewScroll();

            Clear();
            ScheduleUpdateDataAsync();
        }

        private void CreateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool open = false;
            string path = "";
            using (var form = new DatabaseCreationForm(Database))
            {
                form.ShowDialog();
                open = form.OpenAfterFinished;
                path = form.DatabasePath;
            }

            if (open)
            {
                Open(path);
            }
        }

        private void EpdDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new EpdDumpForm(Database))
            {
                form.ShowDialog();
            }
        }

        private void Open(string path)
        {
            Database.Close();
            Database.Open(path);
            QueryExecutor.ResetQueueAndCache();
            UpdateDatabaseInfo();

            OnPositionChanged(this, new EventArgs());
        }

        private void UpdateDatabaseInfo()
        {
            var info = Database.GetInfo();

            if (info.IsOpen)
            {
                int averageMovesPerGame = ((int)Math.Round((double)info.TotalNumPositions() / info.TotalNumGames() / 2.0));

                databaseInfoRichTextBox.Text =
                    "Path: " + info.Path + Environment.NewLine
                    + "Games: " + info.TotalNumGames().ToString("N0")
                    + " | Plies: " + info.TotalNumPositions().ToString("N0") + Environment.NewLine
                    + "Avg game length: " + averageMovesPerGame.ToString("N0") + " moves";
            }
            else
            {
                databaseInfoRichTextBox.Text = "No database open.";
            }
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                using (FolderBrowserDialog browser = new FolderBrowserDialog())
                {
                    if (browser.ShowDialog() == DialogResult.OK)
                    {
                        var path = browser.SelectedPath;

                        Open(path);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Database.Close();
            QueryExecutor.ResetQueueAndCache();
            UpdateDatabaseInfo();
        }

        private void EntriesGridView_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= entriesGridView.Rows.Count || e.ColumnIndex != 0)
            {
                return;
            }

            if (IsEntryDataUpToDate)
            {
                var cell = entriesGridView[e.ColumnIndex, e.RowIndex];
                if (cell.ColumnIndex == 0)
                {
                    var san = cell.Value.ToString();
                    chessBoard.DoMove(san);
                }
            }
        }

        private bool IsInvalidDouble(object value)
        {
            return value == null || value.GetType() != typeof(double) || Double.IsNaN((double)value) || Double.IsInfinity((double)value);
        }

        private void ApplyCommonCellFormatting(DataGridView dgv, DataGridViewCellFormattingEventArgs e)
        {
            var column = dgv.Columns[e.ColumnIndex];
            var columnName = column.Name;
            var headerText = column.HeaderText;

            if (columnName == "Goodness")
            {
                if (IsInvalidDouble(e.Value) || Math.Abs((double)e.Value) < 0.01)
                {
                    e.Value = "";
                }
                else
                {
                    e.Value = ((double)e.Value * 100).ToString("0.0");
                }
                e.FormattingApplied = true;
            }
            else if (columnName == "AvgEloDiff")
            {
                if (IsInvalidDouble(e.Value))
                {
                    e.Value = "";
                }
                else
                {
                    e.Value = e.Value.ToString();
                }
                e.FormattingApplied = true;
            }
            else if (headerText.Contains("%"))
            {
                if (IsInvalidDouble(e.Value))
                {
                    e.Value = "";
                }
                else
                {
                    e.Value = ((double)e.Value * 100).ToString("0.0") + "%";
                }
                e.FormattingApplied = true;
            }
        }

        private void EntriesGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            ApplyCommonCellFormatting(entriesGridView, e);

            if (e.FormattingApplied)
            {
                return;
            }

            if (e.ColumnIndex == 0)
            {
                e.Value = chessBoard.NextMoveNumber() + " " + e.Value.ToString();
                e.FormattingApplied = true;
            }
        }

        private void TotalEntriesGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            ApplyCommonCellFormatting(totalEntriesGridView, e);
        }

        private void EntriesGridView_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            const double goodGoodnessTheshold = 0.95;
            const double badGoodnessTheshold = 0.8;

            var row = entriesGridView.Rows[e.RowIndex];
            var isTransposition = Convert.ToBoolean(row.Cells["IsOnlyTransposition"].Value);
            var goodness = row.Cells["Goodness"].Value;
            var isGoodGoodness = goodness != null && (double)goodness > 0.0 && (double)goodness >= goodGoodnessTheshold * BestGoodness;
            var isBadGoodness = goodness == null || (double)goodness < badGoodnessTheshold * BestGoodness;
            if ((ulong)row.Cells["Count"].Value == 0)
            {
                if (!row.DefaultCellStyle.BackColor.Equals(Color.DarkGray))
                {
                    row.DefaultCellStyle.BackColor = Color.DarkGray;
                }
            }
            else
            {
                if (isTransposition)
                {
                    if (!row.DefaultCellStyle.BackColor.Equals(Color.LightGray))
                    {
                        row.DefaultCellStyle.BackColor = Color.LightGray;
                    }
                }
                else
                {
                    if (!row.DefaultCellStyle.BackColor.Equals(Color.White))
                    {
                        row.DefaultCellStyle.BackColor = Color.White;
                    }
                }

                if (isGoodGoodness)
                {
                    if (!row.Cells[0].Style.ForeColor.Equals(Color.Green))
                    {
                        row.Cells[0].Style.ForeColor = Color.Green;
                    }
                }
                else if (isBadGoodness)
                {
                    if (!row.Cells[0].Style.ForeColor.Equals(Color.Red))
                    {
                        row.Cells[0].Style.ForeColor = Color.Red;
                    }
                }
                else
                {
                    if (!row.Cells[0].Style.ForeColor.Equals(Color.Black))
                    {
                        row.Cells[0].Style.ForeColor = Color.Black;
                    }
                }
            }

            var nextSan = chessBoard.GetNextMoveSan();
            // we replace # as a workaround for now.
            if (nextSan != null && ((MoveWithSan)row.Cells["Move"].Value).San == nextSan.Replace('#', '+'))
            {
                row.DefaultCellStyle.Font = new Font(entriesGridView.Font, FontStyle.Bold);
            }
            else
            {
                if (row.DefaultCellStyle.Font != null && !row.DefaultCellStyle.Font.Equals(entriesGridView.Font))
                {
                    row.DefaultCellStyle.Font = entriesGridView.Font;
                }
            }
        }

        private void TotalEntriesGridView_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            totalEntriesGridView.Columns[e.Column.Index].Width = e.Column.Width;
        }

        private void Application_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (EmbeddedHandler != null)
            {
                EmbeddedHandler.Dispose();
            }
            QueryExecutor.Dispose();

            SaveSettings();
        }

        private void SaveSettings()
        {
            var settings = GatherSerializableSettings();
            settings.Serialize(settingsPath);
        }

        private void LoadSettings()
        {
            var settings = SerializableSettings.Deserialize(settingsPath);
            ApplySerializableSettings(settings);
        }

        private void ApplySerializableSettings(SerializableSettings settings)
        {
            autoQueryCheckbox.Checked = settings.AutoQueryCheckBoxChecked;
            queryEvalCheckBox.Checked = settings.QueryEvalCheckBoxChecked;
            levelHumanCheckBox.Checked = settings.LevelHumanCheckBoxChecked;
            levelEngineCheckBox.Checked = settings.LevelEngineCheckBoxChecked;
            levelServerCheckBox.Checked = settings.LevelServerCheckBoxChecked;
            hideNeverPlayedCheckBox.Checked = settings.HideNeverPlayedCheckBoxChecked;
            typeContinuationsCheckBox.Checked = settings.TypeContinuationsCheckBoxChecked;
            typeTranspositionsCheckBox.Checked = settings.TypeTranspositionsCheckBoxChecked;
            humanWeightCheckbox.Checked = settings.HumanWeightCheckBoxChecked;
            gamesWeightCheckbox.Checked = settings.GamesWeightCheckBoxChecked;
            engineWeightCheckbox.Checked = settings.EngineWeightCheckBoxChecked;
            evaluationWeightCheckbox.Checked = settings.EvaluationWeightCheckBoxChecked;
            combineHECheckbox.Checked = settings.CombineHECheckBoxChecked;
            goodnessUseCountCheckbox.Checked = settings.GoodnessUseCountCheckBoxChecked;
            goodnessNormalizeCheckbox.Checked = settings.GoodnessNormalizeCheckBoxChecked;
            gamesWeightNumericUpDown.Value = settings.GamesWeightNumericUpDownValue;
            humanWeightNumericUpDown.Value = settings.HumanWeightNumericUpDownValue;
            engineWeightNumericUpDown.Value = settings.EngineWeightNumericUpDownValue;
            evalWeightNumericUpDown.Value = settings.EvalWeightNumericUpDownValue;
            splitChessAndData.SplitterDistance = settings.SplitChessAndDataSplitterDistance;
            Width = settings.FormWidth;
            Height = settings.FormHeight;
        }

        private SerializableSettings GatherSerializableSettings()
        {
            return new SerializableSettings
            {
                AutoQueryCheckBoxChecked = autoQueryCheckbox.Checked,
                QueryEvalCheckBoxChecked = queryEvalCheckBox.Checked,
                LevelHumanCheckBoxChecked = levelHumanCheckBox.Checked,
                LevelEngineCheckBoxChecked = levelEngineCheckBox.Checked,
                LevelServerCheckBoxChecked = levelServerCheckBox.Checked,
                HideNeverPlayedCheckBoxChecked = hideNeverPlayedCheckBox.Checked,
                TypeContinuationsCheckBoxChecked = typeContinuationsCheckBox.Checked,
                TypeTranspositionsCheckBoxChecked = typeTranspositionsCheckBox.Checked,
                HumanWeightCheckBoxChecked = humanWeightCheckbox.Checked,
                GamesWeightCheckBoxChecked = gamesWeightCheckbox.Checked,
                EngineWeightCheckBoxChecked = engineWeightCheckbox.Checked,
                EvaluationWeightCheckBoxChecked = evaluationWeightCheckbox.Checked,
                CombineHECheckBoxChecked = combineHECheckbox.Checked,
                GoodnessUseCountCheckBoxChecked = goodnessUseCountCheckbox.Checked,
                GoodnessNormalizeCheckBoxChecked = goodnessNormalizeCheckbox.Checked,
                GamesWeightNumericUpDownValue = gamesWeightNumericUpDown.Value,
                HumanWeightNumericUpDownValue = humanWeightNumericUpDown.Value,
                EngineWeightNumericUpDownValue = engineWeightNumericUpDown.Value,
                EvalWeightNumericUpDownValue = evalWeightNumericUpDown.Value,
                SplitChessAndDataSplitterDistance = splitChessAndData.SplitterDistance,
                FormWidth = Width,
                FormHeight = Height
            };
        }

        private void HideNeverPlayedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Repopulate();
        }

        private void HumanWeightNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (humanWeightCheckbox.Checked)
            {
                UpdateGoodness();
            }
        }

        private void EngineWeightNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (engineWeightCheckbox.Checked)
            {
                UpdateGoodness();
            }
        }

        private void EvalWeightNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (evaluationWeightCheckbox.Checked)
            {
                UpdateGoodness();
            }
        }

        private void GoodnessUseCountCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateGoodness();
        }

        private void GoodnessNormalizeCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateGoodness();
        }

        private void CombineHECheckbox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateGoodness();

            gamesWeightCheckbox.Visible = combineHECheckbox.Checked;
            gamesWeightNumericUpDown.Visible = combineHECheckbox.Checked;
            engineWeightCheckbox.Visible = !combineHECheckbox.Checked;
            engineWeightNumericUpDown.Visible = !combineHECheckbox.Checked;
        }

        private void GamesWeightCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateGoodness();
        }

        private void EngineWeightCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateGoodness();
        }

        private void EvaluationWeightCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateGoodness();
        }

        private void HumanWeightCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateGoodness();
        }

        private void SetupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AnalysisForm == null)
            {
                EmbeddedHandler = new ApplicationEmbeddedAnalysisHandler(this);
                AnalysisForm = new EngineAnalysisForm(EngineProfiles, EmbeddedHandler);
                AnalysisForm.FormClosed += OnAnalysisFormClosed;
                AnalysisForm.Show();
            }
            else if (!AnalysisForm.Visible)
            {
                AnalysisForm.Show();
            }
        }

        private void OnAnalysisFormClosed(object sender, FormClosedEventArgs e)
        {
            if (EmbeddedHandler != null)
            {
                EmbeddedHandler.Dispose();
            }

            EmbeddedHandler = null;
            AnalysisForm = null;
        }

        private void GamesWeightNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (gamesWeightCheckbox.Checked)
            {
                UpdateGoodness();
            }
        }

        private void ProfilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AnalysisForm == null)
            {
                var form = new EngineProfilesForm(EngineProfiles, EngineProfilesFormMode.Manage);
                form.ShowDialog();
            }
            else
            {
                MessageBox.Show("You have to stop analysis to manage engine profiles.");
            }
        }

        private void TotalDataHelpButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "When a database is loaded and queried this table shows 2 rows:\n\n" +
                "Children: The sum of values for each move possible on the board.\n\n" +
                "Root: The values for the position currently on the board. " +
                "Note, this row is often diffrent to Children because of " +
                "transpositions and/or games ending. \n" +
                "For example if the a game ends in this position it will not provide " +
                "values for moves, which makes for less Children games than Root games. " +
                "Alternatively, if some moves transpose and transpositions are not filtered out " +
                "then there will be more Children games than Root games.\n" +
                "It is important to understand that filtering of transpositions/continuations " +
                "works for the Root positions just as it works for moves. " +
                "For example if only continuations are enabled the move that lead " +
                "to the position on the board does matter when querying the database " +
                "and in result the transpositions to the current position will not be included. " +
                "Similarily you can check how many times the current position was obtained solely by transpositions " +
                "by filtering out continuations. For example, by entering moves Nf3 Nf6 Ng1 Ng8 and filtering " +
                "out transpositions you can see how many times the start position was obtained by playing Ng8 and " +
                "what moves were played then.",
                "Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
                );
        }
    }
}
