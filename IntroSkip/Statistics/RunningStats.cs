using System;

namespace IntroSkip.Statistics
{
    public class RunningStats
    {
        private int Mn = 0;
        
        public void Push(double x)
        {
            Mn++;

            // See Knuth TAOCP vol 2, 3rd edition, page 232
            if (Mn == 1)
            {
                MOldM = MNewM = x;
                MOldS = 0.0;
            }
            else
            {
                MNewM = MOldM + (x - MOldM)/Mn;
                MNewS = MOldS + (x - MOldM)*(x - MNewM);
    
                // set up for next iteration
                MOldM = MNewM; 
                MOldS = MNewS;
            }
        }

       
        private double Variance() 
        {
            return ( (Mn > 1) ? MNewS/(Mn - 1) : 0.0 );
        }

        public double StandardDeviation() 
        {
            return Math.Sqrt( Variance() );
        }

        private double MOldM;
        private double MNewM;
        private double MOldS;
        private double MNewS;
    };
}
