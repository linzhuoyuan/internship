﻿/*
 Copyright (C) 2010 Philippe Real (ph_real@hotmail.com)
 Copyright (C) 2020 Jean-Camille Tournier (jean-camille.tournier@avivainvestors.com)

 This file is part of QLNet Project https://github.com/amaggiulli/qlnet

 QLNet is free software: you can redistribute it and/or modify it
 under the terms of the QLNet license.  You should have received a
 copy of the license along with this program; if not, license is
 available at <https://github.com/amaggiulli/QLNet/blob/develop/LICENSE>.

 QLNet is a based on QuantLib, a free-software/open-source library
 for financial quantitative analysts and developers - http://quantlib.org/
 The QuantLib license is available online at http://quantlib.org/license.shtml.

 This program is distributed in the hope that it will be useful, but WITHOUT
 ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 FOR A PARTICULAR PURPOSE.  See the license for more details.
*/

using System;

namespace QLNet
{
   /// <summary>
   /// Chi-square (central and non-central) distributions
   /// </summary>
   public class ChiSquareDistribution
   {
      private double df_;

      public ChiSquareDistribution(double df)
      {
         df_ = df;
      }

      public double value(double x)
      {
         return new GammaDistribution(0.5 * df_).value(0.5 * x);
      }
   }

   public class NonCentralChiSquareDistribution
   {
      private double df_, ncp_;

      public NonCentralChiSquareDistribution(double df, double ncp)
      {
         df_ = df;
         ncp_ = ncp;
      }

      public double value(double x)
      {
         if (x <= 0.0)
            return 0.0;

         double errmax = 1e-12;
         const int itrmax = 10000;
         double lam = 0.5 * ncp_;

         double u = Math.Exp(-lam);
         double v = u;
         double x2 = 0.5 * x;
         double f2 = 0.5 * df_;
         double f_x_2n = df_ - x;

         double t = 0.0;
         if (f2 * Const.QL_EPSILON > 0.125 &&
             Math.Abs(x2 - f2) < Math.Sqrt(Const.QL_EPSILON) * f2)
         {
            t = Math.Exp((1 - t) *
                         (2 - t / (f2 + 1))) / Math.Sqrt(2.0 * Const.M_PI * (f2 + 1.0));
         }
         else
         {
            t = Math.Exp(f2 * Math.Log(x2) - x2 -
                         GammaFunction.logValue(f2 + 1));
         }

         double ans = v * t;

         bool flag = false;
         int n = 1;
         double f_2n = df_ + 2.0;
         f_x_2n += 2.0;

         double bound;
         for (;;)
         {
            if (f_x_2n > 0)
            {
               flag = true;
               bound = t * x / f_x_2n;
               if (bound <= errmax || n > itrmax)
                  goto L_End;
            }
            for (;;)
            {
               u *= lam / n;
               v += u;
               t *= x / f_2n;
               ans += v * t;
               n++;
               f_2n += 2.0;
               f_x_2n += 2.0;
               if (!flag && n <= itrmax)
                  break;

               bound = t * x / f_x_2n;
               if (bound <= errmax || n > itrmax)
                  goto L_End;
            }
         }
         L_End:
         Utils.QL_REQUIRE(bound <= errmax, () => "didn't converge");
         return (ans);
      }
   }

   public class InverseNonCentralChiSquareDistribution
   {
      private NonCentralChiSquareDistribution nonCentralDist_;
      private double guess_;
      private int maxEvaluations_;
      private double accuracy_;

      public InverseNonCentralChiSquareDistribution(double df, double ncp,
                                                    int maxEvaluations,
                                                    double accuracy)
      {
         nonCentralDist_ = new NonCentralChiSquareDistribution(df, ncp);
         guess_ = df + ncp;
         maxEvaluations_ = maxEvaluations;
         accuracy_ = accuracy;
      }

      public InverseNonCentralChiSquareDistribution(double df, double ncp, int maxEvaluations)
         : this(df, ncp, maxEvaluations, 1e-8)
      { }

      public InverseNonCentralChiSquareDistribution(double df, double ncp)
         : this(df, ncp, 10, 1e-8)
      { }

      public double value(double x)
      {
         // first find the right side of the interval
         double upper = guess_;
         int evaluations = maxEvaluations_;
         while (nonCentralDist_.value(upper) < x && evaluations > 0)
         {
            upper *= 2.0;
            --evaluations;
         }

         // use a brent solver for the rest
         Brent solver = new Brent();
         solver.setMaxEvaluations(evaluations);
         return solver.solve(new IncChiQuareFinder(x, nonCentralDist_.value),
                             accuracy_, 0.75 * upper, (evaluations == maxEvaluations_) ? 0.0 : 0.5 * upper,
                             upper);
      }

      class IncChiQuareFinder : ISolver1d
      {
         private double y_;
         Func<double, double> g_;

         public IncChiQuareFinder(double y, Func<double, double> g)
         {
            y_ = y;
            g_ = g;
         }

         public override double value(double x)
         {
            return g_(x) - y_;
         }
      }
   }

   public class CumulativeChiSquareDistribution
   {
      private double df_;

      public CumulativeChiSquareDistribution(double df)
      {
         df_ = df;
      }

      public double value(double x)
      {
         return new CumulativeGammaDistribution(0.5 * df_).value(0.5 * x);
      }
   }

   public class NonCentralCumulativeChiSquareDistribution
   {
      protected double df_, ncp_;

      public NonCentralCumulativeChiSquareDistribution(double df, double ncp)
      {
         df_ = df;
         ncp_ = ncp;
      }

      public double value(double x)
      {
         if (x <= 0.0)
            return 0.0;

         double errmax = 1e-12;
         int itrmax = 10000;
         double lam = 0.5 * ncp_;

         double u = Math.Exp(-lam);
         double v = u;
         double x2 = 0.5 * x;
         double f2 = 0.5 * df_;
         double f_x_2n = df_ - x;

         double t = 0.0;
         if (f2 * Const.QL_EPSILON > 0.125 &&
             Math.Abs(x2 - f2) < Math.Sqrt(Const.QL_EPSILON) * f2)
         {
            t = Math.Exp((1 - t) *
                         (2 - t / (f2 + 1))) / Math.Sqrt(2.0 * Const.M_PI * (f2 + 1.0));
         }
         else
         {
            t = Math.Exp(f2 * Math.Log(x2) - x2 -
                         GammaFunction.logValue(f2 + 1));
         }

         double ans = v * t;

         bool flag = false;
         int n = 1;
         double f_2n = df_ + 2.0;
         f_x_2n += 2.0;

         double bound;
         bool skip = false;
         for (;;)
         {
            if (f_x_2n > 0)
            {
               flag = true;
               skip = true;
            }
            for (;;)
            {
               if (!skip)
               {
                  u *= lam / n;
                  v += u;
                  t *= x / f_2n;
                  ans += v * t;
                  n++;
                  f_2n += 2.0;
                  f_x_2n += 2.0;
                  if (!flag && n <= itrmax)
                     break;
               }
               bound = t * x / f_x_2n;
               skip = false;
               if (bound <= errmax || n > itrmax)
                  goto L_End;
            }
         }
         L_End:
         if (bound > errmax)
            Utils.QL_FAIL("didn't converge");
         return (ans);
      }
   }

   public class NonCentralCumulativeChiSquareSankaranApprox
   {
      protected double df_, ncp_;

      public NonCentralCumulativeChiSquareSankaranApprox(double df, double ncp)
      {
         df_ = df;
         ncp_ = ncp;
      }

      double value(double x)
      {
         double h = 1 - 2 * (df_ + ncp_) * (df_ + 3 * ncp_) / (3 * Math.Pow(df_ + 2 * ncp_, 2));
         double p = (df_ + 2 * ncp_) / Math.Pow(df_ + ncp_, 2);
         double m = (h - 1) * (1 - 3 * h);

         double u = (Math.Pow(x / (df_ + ncp_), h) - (1 + h * p * (h - 1 - 0.5 * (2 - h) * m * p))) /
                    (h * Math.Sqrt(2 * p) * (1 + 0.5 * m * p));

         return new CumulativeNormalDistribution().value(u);
      }
   }


   public class InverseNonCentralCumulativeChiSquareDistribution
   {
      protected NonCentralCumulativeChiSquareDistribution nonCentralDist_;
      protected double guess_;
      protected int maxEvaluations_;
      protected double accuracy_;

      public InverseNonCentralCumulativeChiSquareDistribution(double df, double ncp,
                                                              int maxEvaluations = 10,
                                                              double accuracy = 1e-8)
      {
         nonCentralDist_ = new NonCentralCumulativeChiSquareDistribution(df, ncp);
         guess_ = (df + ncp);
         maxEvaluations_ = maxEvaluations;
         accuracy_ = accuracy;
      }

      public double value(double x)
      {
         // first find the right side of the interval
         double upper = guess_;
         int evaluations = maxEvaluations_;
         while (nonCentralDist_.value(upper) < x && evaluations > 0)
         {
            upper *= 2.0;
            --evaluations;
         }

         // use a Brent solver for the rest
         Brent solver = new Brent();
         ISolver1d f = new MinFinder(nonCentralDist_, x);
         solver.setMaxEvaluations(evaluations);

         return solver.solve(f,
                             accuracy_, 0.75 * upper,
                             (evaluations == maxEvaluations_) ? 0.0 : 0.5 * upper,
                             upper);
      }

      protected class MinFinder : ISolver1d
      {
         protected NonCentralCumulativeChiSquareDistribution nonCentralDist_;
         protected double x_;

         public MinFinder(NonCentralCumulativeChiSquareDistribution nonCentralDist, double x)
         {
            nonCentralDist_ = nonCentralDist;
            x_ = x;
         }

         public override double value(double y)
         {
            return x_ - nonCentralDist_.value(y);
         }
      }
   }
}
