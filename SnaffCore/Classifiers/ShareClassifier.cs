using SnaffCore.Concurrency;
using System;
using System.IO;
using static SnaffCore.Config.Options;

namespace Classifiers
{
    public class ShareClassifier
    {
        private ClassifierRule ClassifierRule { get; set; }

        public ShareClassifier(ClassifierRule inRule)
        {
            this.ClassifierRule = inRule;
        }

        public bool ClassifyShare(string share)
        {
            BlockingMq Mq = BlockingMq.GetMq();

            // first time we hit sysvol, toggle the flag and keep going. every other time, bail out.
            if (share.ToLower().EndsWith("sysvol"))
            {
                if (MyOptions.ScanSysvol == false)
                {
                    return true;
                }
                MyOptions.ScanSysvol = false;
            };
            // same for netlogon
            if (share.ToLower().EndsWith("netlogon"))
            {
                if (MyOptions.ScanNetlogon == false)
                {
                    return true;
                }
                MyOptions.ScanNetlogon = false;
            }
            // check if it matches
            TextClassifier textClassifier = new TextClassifier(ClassifierRule);
            TextResult textResult = textClassifier.TextMatch(share);
            if (textResult != null)
            {
                // if it does, see what we're gonna do with it
                switch (ClassifierRule.MatchAction)
                {
                    case MatchAction.Discard:
                        return true;
                    case MatchAction.Snaffle:
                        // in this context snaffle means 'send a report up the queue but don't scan the share'
                        if (IsShareReadable(share))
                        {
                            ShareResult shareResult = new ShareResult()
                            {
                                Triage = ClassifierRule.Triage,
                                Listable = true,
                                SharePath = share
                            };
                            Mq.ShareResult(shareResult);
                        }
                        return true;
                    default:
                        Mq.Error("You've got a misconfigured share ClassifierRule named " + ClassifierRule.RuleName + ".");
                        return false;
                }
            }
            return false;
        }

        internal bool IsShareReadable(string share)
        {
            BlockingMq Mq = BlockingMq.GetMq();
            try
            {
                string[] files = Directory.GetFiles(share);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception e)
            {
                Mq.Trace(e.ToString());
            }
            return false;
        }
    }

    public class ShareResult
    {
        public bool Snaffle { get; set; }
        public bool ScanShare { get; set; }
        public string SharePath { get; set; }
        public bool Listable { get; set; }
        public Triage Triage { get; set; } = Triage.Green;
    }
}