using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestApp
{
    class FeasibleDefs
    {        
        int i;
        public void Feasibility(int local)
        {
            this.i = 20;

            if (local * 20 > 60)
                this.i = 50;
            else
                this.i = 200;

            int localVal = this.i;
        }

        public int GetI1()
        {
            return this.i;
        }

        public int GetI2()
        {
            return this.i;
        }

        public int GetI3()
        {
            return this.i;
        }

        public void OtherDefs1()
        {
            this.i = 90;
        }

        public void OtherDefs2()
        {
            this.i = 90;
        }

        public void OtherDefs3()
        {
            this.i = 90;
        }
    }
}
