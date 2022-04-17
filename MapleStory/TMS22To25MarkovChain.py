import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
chances = np.array([[0,1,0,0],
                    [0.98,0,0.02,0],
                    [0,0.99,0,0.01],
                    [0,0,0,1]])
state = np.array([[0,1,0,0]])
stateHist = state
dfStateHist = pd.DataFrame(state)
distr_hist = [[0,1,0,0]]
totalPrice=0
prices = np.array([[150000],[2100],[2100],[0]])
count=0
while state[0][3]<0.9999:
    count+=1
    priceThisRound=np.dot(state,prices)[0][0]
    totalPrice+=priceThisRound
    state = np.dot(state,chances)
    print(f"% at 25 {state[0][3]}, price this round: {priceThisRound}, total price: {totalPrice} state: {state}")
    stateHist=np.append(stateHist,state,axis=0)
    dfDistrHist = pd.DataFrame(stateHist)
print(f"totalPrice:{totalPrice} taps:{count}")
dfDistrHist.plot()
plt.show()
