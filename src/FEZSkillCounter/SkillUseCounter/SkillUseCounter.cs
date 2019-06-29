﻿using SkillUseCounter.Algorithm;
using SkillUseCounter.Entity;
using SkillUseCounter.Recognizer;
using SkillUseCounter.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SkillUseCounter
{
    /// <summary>
    /// スキル使用を通知するサービス
    /// </summary>
    public class SkillCountService
    {
        /// <summary>
        /// 処理速度(直近100回)を通知
        /// </summary>
        public event EventHandler<double>      ProcessTimeUpdated;

        /// <summary>
        /// スキル一覧の更新通知
        /// </summary>
        public event EventHandler<Skill[]>     SkillsUpdated;

        /// <summary>
        /// Powの更新通知
        /// </summary>
        public event EventHandler<int>         PowUpdated;

        /// <summary>
        /// Powのデバフ(パワブレなど)更新通知
        /// </summary>
        public event EventHandler<PowDebuff[]> PowDebuffsUpdated;

        /// <summary>
        /// スキル使用を通知
        /// </summary>
        public event EventHandler<Skill>       SkillUsed;

        private PreRecognizer            _preRecognizer            = new PreRecognizer();
        private WarStateRecognizer       _warStateRecognizer       = new WarStateRecognizer(new MapRecognizer());
        private SkillArrayRecognizer     _skillArrayRecognizer     = new SkillArrayRecognizer();
        private PowDebuffArrayRecognizer _powDebuffArrayRecognizer = new PowDebuffArrayRecognizer();
        private PowRecognizer            _powRecognizer            = new PowRecognizer();

        private FEZScreenShotStorage     _screenShotStorage        = new FEZScreenShotStorage();
        private SkillUseAlgorithm        _skillCountAlgorithm      = new SkillUseAlgorithm();

        private CancellationTokenSource _cts  = null;
        private Task                    _task = null;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public SkillCountService()
        {
            SkillStorage.Create();
            PowStorage.Create();
            MapStorage.Create();

            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                Logger.WriteLine(e.Exception.ToString());
            };

            _skillArrayRecognizer.Updated     += (_, e) => SkillsUpdated?.Invoke(this, e);
            _powRecognizer.Updated            += (_, e) => PowUpdated?.Invoke(this, e);
            _powDebuffArrayRecognizer.Updated += (_, e) => PowDebuffsUpdated?.Invoke(this, e);

            Logger.WriteLine("起動");
        }

        /// <summary>
        /// スキル使用の監視開始
        /// </summary>
        public void Start()
        {
            if (_cts != null)
            {
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _task = Task.Run(() => { Run(token); }, token);
            }
            catch
            {
                // TODO: errorハンドリング
                Stop();
                throw;
            }
        }

        /// <summary>
        /// スキル使用の監視停止
        /// </summary>
        public void Stop()
        {
            if (_cts == null)
            {
                return;
            }

            try
            {
                _cts.Cancel(false);
                _task.Wait();
            }
            catch
            {
                // TODO: errorハンドリング
                throw;
            }
            finally
            {
                _cts.Dispose();
                _cts  = null;
                _task = null;
            }
        }

        private void Run(CancellationToken token)
        {
            var stopwatch    = Stopwatch.StartNew();
            var processTimes = new List<long>(100);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 解析
                    var start  = stopwatch.ElapsedMilliseconds;
                    var result = Analyze();
                    var end    = stopwatch.ElapsedMilliseconds;

                    // 解析時間更新
                    processTimes.Add(end - start);
                    if (processTimes.Count > 100)
                    {
                        ProcessTimeUpdated?.Invoke(this, processTimes.Average());
                        processTimes.Clear();
                    }

                    // 処理が失敗している場合は大抵即終了している。
                    // ループによるCPU使用率を抑えるためにwait
                    var waitTime = 30 - (int)(end - start);
                    if (!result && waitTime > 0)
                    {
                        Thread.Sleep(waitTime);
                    }
                }
                catch (OperationCanceledException)
                { }
                catch
                {
                    // TODO: errorハンドリング
                    throw;
                }
            }
        }

        private bool Analyze()
        {
            using (var screenShot = _screenShotStorage.Shoot())
            {
                // 解析可能か確認
                if (!_preRecognizer.Recognize(screenShot.Image))
                {
                    return false;
                }

                // 現在のPow・スキル・デバフを取得
                var pow        = _powRecognizer.Recognize(screenShot.Image);
                var skills     = _skillArrayRecognizer.Recognize(screenShot.Image);
                var powDebuffs = _powDebuffArrayRecognizer.Recognize(screenShot.Image);

                // 選択中スキルを取得
                var activeSkill = skills.FirstOrDefault(x => x.IsActive);

                // 取得失敗していれば終了
                if (pow         == PowRecognizer.InvalidPow ||
                    skills      == SkillArrayRecognizer.InvalidSkills ||
                    powDebuffs  == PowDebuffArrayRecognizer.InvalidPowDebuffs ||
                    activeSkill == default(Skill))
                {
                    return false;
                }

                // スキルを使ったかどうかチェック
                var isSkillUsed = _skillCountAlgorithm.IsSkillUsed(
                    screenShot.TimeStamp,
                    pow,
                    activeSkill,
                    powDebuffs);

                // スキルを使っていれば更新通知
                if (isSkillUsed)
                {
                    SkillUsed?.BeginInvoke(
                        this,
                        activeSkill,
                        null,
                        null);
                }
            }

            return true;
        }
    }
}
