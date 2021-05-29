import { companyActions } from "./company-slice";
import { tokenConfig } from "../utils/tokenUtility";

import { notifyError, notifySuccess } from '../components/Toast/Toast';
import { registerUser } from "./user-actions";

import API from '../utils/api';


export const addCompany = (item, userInfo, callback) => dispatch => {

    dispatch(companyActions.start_loading());

    let config = tokenConfig();

    API.post('api/company', item, config)
        .then(res => {
            dispatch(companyActions.add_company_success(res.data));

            userInfo.companyId = res.data.data;
            dispatch(registerUser(userInfo));
            callback();
            notifySuccess(res.data.message);
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(companyActions.error());
            notifyError("Error occured while adding Company");
        })
};

export const updateCompany = (item) => dispatch => {

    dispatch(companyActions.start_loading());

    let config = tokenConfig();
    console.log(item);
    API.put('api/company', item, config)
        .then(res => {
            console.log(res);
            dispatch(companyActions.update_company_sucess(res.data));
            notifySuccess("Company updated successfully");
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(companyActions.error());
            notifyError("Error occured while adding Company");
        })
};

export const deleteCompany = (item) => dispatch => {

    dispatch(companyActions.start_loading());

    let config = tokenConfig();
    console.log(config);
    API.patch(`api/company/${item.id}`, {}, config)
        .then(res => {
            console.log(res);
            dispatch(companyActions.update_company_sucess(res.data));
            notifySuccess("Company deleted successfully");
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(companyActions.error());
            notifyError("Error occured while deleting Company");
        })
};

export const getCompany = (item = { pageNumber: 1, pageSize: 10 }) => (dispatch) => {

    dispatch(companyActions.start_loading());
    console.log(item);
    let config = tokenConfig();

    API.get(`api/company?pageNumber=${item.pageNumber}&pageSize=${item.pageSize}`, config)
        .then(res => {
            dispatch(companyActions.load_company_success(res.data));
        })
        .catch(err => {
            console.log(err.response);
            dispatch(companyActions.error());
        });
};