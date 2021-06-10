import { siteActions } from "./site-slice";
import { tokenConfig } from "../utils/tokenUtility";

import { notifyError, notifySuccess } from '../components/Toast/Toast';

import API from '../utils/api';

export const addSite = (item, callback = () => { }) => dispatch => {

    dispatch(siteActions.start_loading());

    let config = tokenConfig();

    API.post('api/site', item, config)
        .then(res => {
            dispatch(siteActions.add_site_success(res.data));
            callback();
            notifySuccess(res.data.message);
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(siteActions.error());
            notifyError("Error occured while adding Site");
        })
};

export const updateSite = (item) => dispatch => {

    dispatch(siteActions.start_loading());

    let config = tokenConfig();
    console.log(item);
    API.put('api/site', item, config)
        .then(res => {
            console.log(res);
            dispatch(siteActions.update_site_sucess(res.data));
            notifySuccess("Site updated successfully");
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(siteActions.error());
            notifyError("Error occured while updating Site");
        })
};

export const deleteSite = (item) => dispatch => {

    dispatch(siteActions.start_loading());

    let config = tokenConfig();
    console.log(item);
    API.patch(`api/site/${item.id}`, {}, config)
        .then(res => {
            console.log(res);
            dispatch(siteActions.update_site_sucess(res.data));
            notifySuccess("Site deleted successfully");
        })
        .catch(err => {
            console.log(err.response.data);
            dispatch(siteActions.error());
            notifyError("Error occured while adding Site");
        })
};

export const getSite = (companyId, item = { pageNumber: 1, pageSize: 10 }) => (dispatch) => {

    dispatch(siteActions.start_loading());    
    let config = tokenConfig();    
    API.get(`api/site/${companyId}?pageNumber=${item.pageNumber}&pageSize=${item.pageSize}`, config)
        .then(res => {
            dispatch(siteActions.load_site_success({...res.data, method : "getSite"}));            
        })
        .catch(err => {
            console.log(err.response);
            dispatch(siteActions.error());
        });
};

export const getSiteManager = ( item = { pageNumber: 1, pageSize: 10 }) => (dispatch) => {

    dispatch(siteActions.start_loading());    
    let config = tokenConfig();    
    API.get(`api/site/manager/?pageNumber=${item.pageNumber}&pageSize=${item.pageSize}`, config)
        .then(res => {
            dispatch(siteActions.load_site_success({...res.data, method : "getSite"}));            
        })
        .catch(err => {
            console.log(err.response);
            dispatch(siteActions.error());
        });
};


export const getSiteWithoutPagination = (companyId) => (dispatch) => {

    dispatch(siteActions.start_loading());    
    let config = tokenConfig();    
    API.get(`api/site/all/${companyId}`, config)
        .then(res => {
            dispatch(siteActions.load_site_success({data: res.data, totalRecords:0, method: "getSiteWithoutPagination" }));            
        })
        .catch(err => {
            console.log(err.response);
            dispatch(siteActions.error());
        });
};

export const getSiteById = (siteId) => (dispatch) => {    

    let config = tokenConfig();

    API.get(`api/site/id/${siteId}`, config)
        .then(res => {            
            dispatch(siteActions.load_siteById_success(res.data));
        })
        .catch(err => {
            console.log(err.response);
            dispatch(siteActions.error());
        });
};
