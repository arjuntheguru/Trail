import { configureStore } from '@reduxjs/toolkit';
import authReducer from './auth-slice';
import companyRedcuer from './company-slice';
import userReducer from './user-slice';
import siteReducer from './site-slice';
import taskReducer from './task-slice';

const store = configureStore({
    reducer : {
        auth : authReducer,
        company : companyRedcuer,
        user : userReducer,
        site : siteReducer,
        task : taskReducer
    }
});

export default store;